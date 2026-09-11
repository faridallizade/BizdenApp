namespace Bizden.Domain.Entities;

public sealed class SharedGallery
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PinHash { get; set; } = string.Empty;
    public DateTimeOffset EnabledAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Event Event { get; set; } = null!;
    public ICollection<SharedGalleryPhoto> Photos { get; set; } = new List<SharedGalleryPhoto>();
}
