using Bizden.Domain.Enums;

namespace Bizden.Domain.Entities;

public sealed class Photo
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid InvitationId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public PhotoStatus Status { get; set; } = PhotoStatus.PendingUpload;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? StorageDeletedAt { get; set; }
    public string? ThumbnailStorageKey { get; set; }
    public string? PreviewStorageKey { get; set; }
    public DateTimeOffset? MediaProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    public Event Event { get; set; } = null!;
    public Invitation Invitation { get; set; } = null!;
    public UploadReservation? UploadReservation { get; set; }
    public ICollection<SharedGalleryPhoto> SharedGalleries { get; set; } = new List<SharedGalleryPhoto>();
}
