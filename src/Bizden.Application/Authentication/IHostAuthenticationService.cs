using Bizden.Domain.Entities;

namespace Bizden.Application.Authentication;

public interface IHostAuthenticationService
{
    Task<HostAuthenticationResult> RegisterAsync(RegisterHostCommand command, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> AuthenticateAsync(LoginHostCommand command, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> VerifyEmailAsync(VerifyHostEmailCommand command, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> ResendVerificationAsync(string email, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> ResetPasswordAsync(ResetPasswordCommand command, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> UpdateProfileAsync(Guid hostUserId, UpdateHostProfileCommand command, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> RequestEmailChangeAsync(Guid hostUserId, string email, CancellationToken cancellationToken);
    Task<HostAuthenticationResult> ConfirmEmailChangeAsync(Guid hostUserId, string code, CancellationToken cancellationToken);
}

public sealed record RegisterHostCommand(string Name, string Email, string Password);
public sealed record LoginHostCommand(string Email, string Password);
public sealed record VerifyHostEmailCommand(string Email, string Code);
public sealed record ResetPasswordCommand(string Email, string Code, string Password);
public sealed record UpdateHostProfileCommand(string Name);
public sealed record HostAuthenticationResult(HostUser? User, string? ErrorCode, bool RequiresEmailVerification = false)
{
    public bool Succeeded => User is not null;
}
