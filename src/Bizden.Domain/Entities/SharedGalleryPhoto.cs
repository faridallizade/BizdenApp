namespace Bizden.Domain.Entities;

public sealed class SharedGalleryPhoto
{
    public Guid SharedGalleryId { get; set; }
    public Guid PhotoId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public SharedGallery SharedGallery { get; set; } = null!;
    public Photo Photo { get; set; } = null!;
}
