using Bizden.Application.Authentication;
using Bizden.Application.Auditing;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Bizden.Infrastructure.Authentication;

public sealed class HostAuthenticationService(BizdenDbContext dbContext, IAuditLogService audit, IEmailSender emailSender) : IHostAuthenticationService
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
            if (!await emailSender.SendVerificationCodeAsync(user.Email, code, cancellationToken)) return new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
            return new HostAuthenticationResult(null, null, true);
        }

        user.EmailVerificationCodeHash = null;
        user.EmailVerificationExpiresAt = null;
        user.EmailVerificationSentAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
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
        return await emailSender.SendVerificationCodeAsync(user.Email, code, cancellationToken)
            ? new HostAuthenticationResult(null, null, true)
            : new HostAuthenticationResult(null, "EMAIL_DELIVERY_UNAVAILABLE", true);
    }

    private static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    private static bool IsTestDomain(string email) => email.LastIndexOf('@') is var separator && separator > 0 && string.Equals(email[(separator + 1)..], "farid.test", StringComparison.OrdinalIgnoreCase);
}
