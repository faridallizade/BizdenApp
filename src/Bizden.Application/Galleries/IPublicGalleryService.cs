namespace Bizden.Application.Galleries;

public interface IPublicGalleryService
{
    Task<PublicGalleryInfo?> GetInfoAsync(Guid publicId, CancellationToken cancellationToken);
    Task<PublicGallery?> UnlockAsync(Guid publicId, string pin, CancellationToken cancellationToken);
}

public sealed record PublicGalleryInfo(string EventName, string? Description, DateTimeOffset EventDate, string? BrandColor, string? CustomMessage, string? CoverUrl);
public sealed record PublicGallery(string EventName, string? Description, DateTimeOffset EventDate, string? BrandColor, string? CustomMessage, string? CoverUrl, IReadOnlyList<PublicGalleryPhoto> Photos);
public sealed record PublicGalleryPhoto(Guid Id, string OriginalFileName, DateTimeOffset UploadedAt, string? ThumbnailUrl, string? PreviewUrl);
