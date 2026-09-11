namespace Bizden.Domain.Entities;

public sealed class HostUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsAdmin { get; set; }
    public DateTimeOffset? BlockedAt { get; set; }
    public string? BlockedReason { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string? EmailVerificationCodeHash { get; set; }
    public DateTimeOffset? EmailVerificationExpiresAt { get; set; }
    public DateTimeOffset? EmailVerificationSentAt { get; set; }
    public string? PasswordResetCodeHash { get; set; }
    public DateTimeOffset? PasswordResetCodeExpiresAt { get; set; }
    public DateTimeOffset? PasswordResetCodeSentAt { get; set; }
    public string? PendingEmail { get; set; }
    public string? PendingNormalizedEmail { get; set; }
    public string? EmailChangeCodeHash { get; set; }
    public DateTimeOffset? EmailChangeCodeExpiresAt { get; set; }
    public DateTimeOffset? EmailChangeCodeSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
