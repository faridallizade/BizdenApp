namespace Bizden.Domain.Entities;

public sealed class Invitation
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int UploadLimit { get; set; }
    public int ReservedUploads { get; set; }
    public int CompletedUploads { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Event Event { get; set; } = null!;
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<UploadReservation> UploadReservations { get; set; } = new List<UploadReservation>();
}
