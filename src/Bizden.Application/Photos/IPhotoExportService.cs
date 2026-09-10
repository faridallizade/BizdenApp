namespace Bizden.Application.Photos;

public interface IPhotoExportService
{
    Task<PhotoExportJobSummary?> QueueAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PhotoExportJobSummary>?> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
}

public sealed record PhotoExportJobSummary(Guid Id, string Status, string? FileName, string? DownloadUrl, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt);
