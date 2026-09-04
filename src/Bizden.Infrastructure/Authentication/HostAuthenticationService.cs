using Bizden.Application.Authentication;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Authentication;

public sealed class HostAuthenticationService(BizdenDbContext dbContext) : IHostAuthenticationService
{
    private readonly PasswordHasher<HostUser> passwordHasher = new();

    public async Task<HostAuthenticationResult> RegisterAsync(RegisterHostCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        var email = command.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || string.IsNullOrWhiteSpace(email) || email.Length > 256 || command.Password.Length < 12)
        {
            return new HostAuthenticationResult(null, "INVALID_REGISTRATION");
        }

        if (await dbContext.HostUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return new HostAuthenticationResult(null, "EMAIL_ALREADY_REGISTERED");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new HostUser
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            NormalizedEmail = normalizedEmail,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, command.Password);

        dbContext.HostUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new HostAuthenticationResult(user, null);
    }

    public async Task<HostAuthenticationResult> AuthenticateAsync(LoginHostCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await dbContext.HostUsers.SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
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

        return new HostAuthenticationResult(user, null);
    }
}
