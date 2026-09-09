namespace Bizden.Domain.Entities;

public sealed class HostUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string? EmailVerificationCodeHash { get; set; }
    public DateTimeOffset? EmailVerificationExpiresAt { get; set; }
    public DateTimeOffset? EmailVerificationSentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
