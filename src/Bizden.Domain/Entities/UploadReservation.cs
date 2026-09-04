using Bizden.Domain.Enums;

namespace Bizden.Domain.Entities;

public sealed class UploadReservation
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
    public Guid PhotoId { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Invitation Invitation { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}
