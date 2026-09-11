using Bizden.Domain.Enums;

namespace Bizden.Domain.Entities;

public sealed class Event
{
    public Guid Id { get; set; }
    public Guid PublicId { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageKey { get; set; }
    public string? BrandColor { get; set; }
    public string? CustomMessage { get; set; }
    public string? GalleryPinHash { get; set; }
    public DateTimeOffset? GalleryEnabledAt { get; set; }
    public DateTimeOffset EventDate { get; set; }
    public string TimeZone { get; set; } = "Asia/Baku";
    public DateTimeOffset UploadStartAt { get; set; }
    public DateTimeOffset UploadEndAt { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<SharedGallery> SharedGalleries { get; set; } = new List<SharedGallery>();
}
