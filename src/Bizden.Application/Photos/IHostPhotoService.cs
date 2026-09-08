namespace Bizden.Application.Photos;

public interface IHostPhotoService
{
    Task<HostPhotoPage?> ListAsync(Guid ownerId, Guid eventId, Guid? invitationId, int page, int pageSize, CancellationToken cancellationToken);
    Task<HostPhotoDownload?> GetDownloadAsync(Guid ownerId, Guid photoId, CancellationToken cancellationToken);
    Task<HostPhotoExport?> CreateExportAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid ownerId, Guid photoId, CancellationToken cancellationToken);
    Task DeleteStoredObjectsAsync(CancellationToken cancellationToken);
}

public sealed record HostPhotoPage(IReadOnlyList<HostPhotoItem> Items, int Page, int PageSize, int TotalCount);
public sealed record HostPhotoItem(Guid Id, Guid InvitationId, string? InvitationLabel, string OriginalFileName, string MimeType, long FileSize, DateTimeOffset UploadedAt, string? ThumbnailUrl, string? PreviewUrl);
public sealed record HostPhotoDownload(string OriginalFileName, string MimeType, string Url);
public sealed record HostPhotoExport(string FileName, byte[] Content);
