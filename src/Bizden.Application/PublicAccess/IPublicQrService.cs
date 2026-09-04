namespace Bizden.Application.PublicAccess;

public interface IPublicQrService
{
    Task<PublicQrView> GetAsync(string token, CancellationToken cancellationToken);
    Task<ReservationResult> ReserveAsync(string token, ReserveUploadCommand command, CancellationToken cancellationToken);
    Task CancelAsync(string token, Guid reservationId, CancellationToken cancellationToken);
    Task<UploadUrlResult> PrepareUploadAsync(string token, Guid reservationId, CancellationToken cancellationToken);
    Task<ReservationResult> CompleteUploadAsync(string token, Guid reservationId, CancellationToken cancellationToken);
    Task ExpireReservationsAsync(CancellationToken cancellationToken);
}

public sealed record PublicQrView(string State, string? EventName, string? Description, DateTimeOffset? EventDate, string? TimeZone, int RemainingPhotos, int? UploadLimit, DateTimeOffset? UploadEndAt);
public sealed record ReserveUploadCommand(string FileName, string MimeType, long FileSize, string IdempotencyKey);
public sealed record ReservationResult(string State, Guid? ReservationId, DateTimeOffset? ExpiresAt, int RemainingPhotos);
public sealed record UploadUrlResult(string State, string? Url, DateTimeOffset? ExpiresAt);
