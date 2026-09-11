using Bizden.Application.Authentication;
using Bizden.Application.Auditing;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Bizden.Infrastructure.Authentication;

public sealed class HostAuthenticationService(BizdenDbContext dbContext, IAuditLogService audit, IEmailSender emailSender, IConfiguration configuration) : IHostAuthenticationService
{
    private readonly PasswordHasher<HostUser> passwordHasher = new();

    public async Task<HostAuthenticationResult> RegisterAsync(RegisterHostCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        var email = command.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || string.IsNullOrWhiteSpace(email) || email.Length > 256 || command.Password.Length < 8 || !command.Password.Any(char.IsDigit))
        {
            return new HostAuthenticationResult(null, "INVALID_REGISTRATION");
        }

        var existingUser = await dbContext.HostUsers.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existingUser is not null && existingUser.EmailVerifiedAt is not null)
        {
            return new HostAuthenticationResult(null, "EMAIL_ALREADY_REGISTERED");
        }

        var now = DateTimeOffset.UtcNow;
        var bypassVerification = IsTestDomain(email);
        var user = existingUser ?? new HostUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = normalizedEmail,
            CreatedAt = now
        };
        user.Name = name;
        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.IsActive = bypassVerification;
        user.EmailVerifiedAt = bypassVerification ? now : null;
        user.UpdatedAt = now;
        user.PasswordHash = passwordHasher.HashPassword(user, command.Password);

        if (existingUser is null) dbContext.HostUsers.Add(user);
        if (!bypassVerification)
        {
            var code = GenerateCode();
            user.EmailVerificationCodeHash = passwordHasher.HashPassword(user, code);
            user.EmailVerificationExpiresAt = now.AddMinutes(15);
            user.EmailVerificationSentAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (!await emailSender.SendOneTimeCodeAsync(user.Email, EmailCodePurpose.Verification, code, cancellationToken)) return new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
            return new HostAuthenticationResult(null, null, true);
        }

        user.EmailVerificationCodeHash = null;
        user.EmailVerificationExpiresAt = null;
        user.EmailVerificationSentAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapAdminAsync(user, cancellationToken);
        await audit.RecordAsync(user.Id, "Host", "HostRegistered", "HostUser", user.Id, null, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> AuthenticateAsync(LoginHostCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || user.EmailVerifiedAt is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new HostAuthenticationResult(null, "INVALID_CREDENTIALS");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return new HostAuthenticationResult(null, "INVALID_CREDENTIALS");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, command.Password);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await audit.RecordAsync(user.Id, "Host", "HostAuthenticated", "HostUser", user.Id, null, cancellationToken);
        await EnsureBootstrapAdminAsync(user, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> VerifyEmailAsync(VerifyHostEmailCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null || string.IsNullOrWhiteSpace(user.EmailVerificationCodeHash) || user.EmailVerificationExpiresAt <= DateTimeOffset.UtcNow)
            return new HostAuthenticationResult(null, "INVALID_OR_EXPIRED_CODE", true);
        if (command.Code.Length != 6 || passwordHasher.VerifyHashedPassword(user, user.EmailVerificationCodeHash, command.Code) == PasswordVerificationResult.Failed)
            return new HostAuthenticationResult(null, "INVALID_OR_EXPIRED_CODE", true);

        user.IsActive = true;
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationExpiresAt = null;
        user.EmailVerificationSentAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBootstrapAdminAsync(user, cancellationToken);
        await audit.RecordAsync(user.Id, "Host", "HostEmailVerified", "HostUser", user.Id, null, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> ResendVerificationAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || user.EmailVerifiedAt is not null || IsTestDomain(user.Email)) return new HostAuthenticationResult(null, "INVALID_VERIFICATION_REQUEST", true);
        if (user.EmailVerificationSentAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return new HostAuthenticationResult(null, "VERIFICATION_RATE_LIMITED", true);

        var code = GenerateCode();
        user.EmailVerificationCodeHash = passwordHasher.HashPassword(user, code);
        user.EmailVerificationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        user.EmailVerificationSentAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await emailSender.SendOneTimeCodeAsync(user.Email, EmailCodePurpose.Verification, code, cancellationToken)
            ? new HostAuthenticationResult(null, null, true)
            : new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
    }

    public async Task<HostAuthenticationResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);
        // Deliberately return the same successful response for unknown or blocked accounts.
        if (user is null || !user.IsActive || user.EmailVerifiedAt is null) return new HostAuthenticationResult(null, null, true);
        if (user.PasswordResetCodeSentAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return new HostAuthenticationResult(null, "RESET_RATE_LIMITED", true);

        var code = GenerateCode();
        user.PasswordResetCodeHash = passwordHasher.HashPassword(user, code);
        user.PasswordResetCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        user.PasswordResetCodeSentAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await emailSender.SendOneTimeCodeAsync(user.Email, EmailCodePurpose.PasswordReset, code, cancellationToken)
            ? new HostAuthenticationResult(null, null, true)
            : new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
    }

    public async Task<HostAuthenticationResult> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!IsValidPassword(command.Password)) return new HostAuthenticationResult(null, "INVALID_PASSWORD");
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordResetCodeHash) || user.PasswordResetCodeExpiresAt <= DateTimeOffset.UtcNow ||
            command.Code.Length != 6 || passwordHasher.VerifyHashedPassword(user, user.PasswordResetCodeHash, command.Code) == PasswordVerificationResult.Failed)
            return new HostAuthenticationResult(null, "INVALID_OR_EXPIRED_CODE");

        user.PasswordHash = passwordHasher.HashPassword(user, command.Password);
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresAt = null;
        user.PasswordResetCodeSentAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(user.Id, "Host", "HostPasswordReset", "HostUser", user.Id, null, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> UpdateProfileAsync(Guid hostUserId, UpdateHostProfileCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) return new HostAuthenticationResult(null, "INVALID_PROFILE");
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.Id == hostUserId, cancellationToken);
        if (user is null || !user.IsActive) return new HostAuthenticationResult(null, "HOST_NOT_FOUND");
        user.Name = name;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(user.Id, "Host", "HostProfileUpdated", "HostUser", user.Id, null, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> RequestEmailChangeAsync(Guid hostUserId, string email, CancellationToken cancellationToken)
    {
        var candidateEmail = email.Trim();
        var normalizedEmail = candidateEmail.ToUpperInvariant();
        if (!IsValidEmail(candidateEmail)) return new HostAuthenticationResult(null, "INVALID_EMAIL");
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.Id == hostUserId, cancellationToken);
        if (user is null || !user.IsActive) return new HostAuthenticationResult(null, "HOST_NOT_FOUND");
        if (normalizedEmail == user.NormalizedEmail) return new HostAuthenticationResult(null, "EMAIL_UNCHANGED");
        if (await dbContext.HostUsers.AnyAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken)) return new HostAuthenticationResult(null, "EMAIL_ALREADY_REGISTERED");
        if (user.EmailChangeCodeSentAt > DateTimeOffset.UtcNow.AddMinutes(-1)) return new HostAuthenticationResult(null, "EMAIL_CHANGE_RATE_LIMITED", true);

        if (IsTestDomain(candidateEmail))
        {
            user.Email = candidateEmail;
            user.NormalizedEmail = normalizedEmail;
            user.PendingEmail = null;
            user.PendingNormalizedEmail = null;
            user.EmailChangeCodeHash = null;
            user.EmailChangeCodeExpiresAt = null;
            user.EmailChangeCodeSentAt = null;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await audit.RecordAsync(user.Id, "Host", "HostEmailChanged", "HostUser", user.Id, null, cancellationToken);
            return new HostAuthenticationResult(user, null);
        }

        var code = GenerateCode();
        user.PendingEmail = candidateEmail;
        user.PendingNormalizedEmail = normalizedEmail;
        user.EmailChangeCodeHash = passwordHasher.HashPassword(user, code);
        user.EmailChangeCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        user.EmailChangeCodeSentAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await emailSender.SendOneTimeCodeAsync(candidateEmail, EmailCodePurpose.EmailChange, code, cancellationToken)
            ? new HostAuthenticationResult(null, null, true)
            : new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
    }

    public async Task<HostAuthenticationResult> ConfirmEmailChangeAsync(Guid hostUserId, string code, CancellationToken cancellationToken)
    {
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.Id == hostUserId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PendingEmail) || string.IsNullOrWhiteSpace(user.PendingNormalizedEmail) ||
            string.IsNullOrWhiteSpace(user.EmailChangeCodeHash) || user.EmailChangeCodeExpiresAt <= DateTimeOffset.UtcNow || code.Length != 6 ||
            passwordHasher.VerifyHashedPassword(user, user.EmailChangeCodeHash, code) == PasswordVerificationResult.Failed)
            return new HostAuthenticationResult(null, "INVALID_OR_EXPIRED_CODE");

        user.Email = user.PendingEmail;
        user.NormalizedEmail = user.PendingNormalizedEmail;
        user.PendingEmail = null;
        user.PendingNormalizedEmail = null;
        user.EmailChangeCodeHash = null;
        user.EmailChangeCodeExpiresAt = null;
        user.EmailChangeCodeSentAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(user.Id, "Host", "HostEmailChanged", "HostUser", user.Id, null, cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static bool IsValidPassword(string password) => password.Length >= 8 && password.Any(char.IsDigit);
    private static bool IsValidEmail(string email) => !string.IsNullOrWhiteSpace(email) && email.Length <= 256 && email.Contains('@');
    private static bool IsTestDomain(string email) => email.LastIndexOf('@') is var separator && separator > 0 && string.Equals(email[(separator + 1)..], "farid.test", StringComparison.OrdinalIgnoreCase);

    private async Task EnsureBootstrapAdminAsync(HostUser user, CancellationToken cancellationToken)
    {
        var candidates = (configuration["Admin:BootstrapEmails"] ?? "bizdenapp@gmail.com;alizadebuzinez@gmail.com")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(email => email.ToUpperInvariant()).ToList();
        if (candidates.Count == 0) return;
        var existingEmails = await dbContext.HostUsers.AsNoTracking().Where(candidate => candidates.Contains(candidate.NormalizedEmail)).Select(candidate => candidate.NormalizedEmail).ToListAsync(cancellationToken);
        var selected = candidates.FirstOrDefault(existingEmails.Contains);
        if (selected == user.NormalizedEmail && !user.IsAdmin)
        {
            user.IsAdmin = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
