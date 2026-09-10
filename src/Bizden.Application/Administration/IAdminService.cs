namespace Bizden.Application.Administration;

public interface IAdminService
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AdminHostPage> ListHostsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminHost?> UpdateHostAsync(Guid hostId, UpdateAdminHostCommand command, CancellationToken cancellationToken);
    Task<AdminEventPage> ListEventsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> SoftDeleteEventAsync(Guid eventId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record UpdateAdminHostCommand(bool IsActive, bool IsBlocked, string? BlockedReason);
public sealed record AdminDashboard(int TotalHosts, int ActiveHosts, int TotalEvents, int TotalPhotos, long PhotoBytes, int TotalQrCodes, int UsedQrCodes, int PublicGalleries, IReadOnlyList<AdminMonthlyMetric> Monthly, IReadOnlyList<AdminEventMetric> Events);
public sealed record AdminMonthlyMetric(string Month, int Events, int Photos, int QrCodes, int UsedQrCodes, long PhotoBytes);
public sealed record AdminEventMetric(Guid EventId, string EventName, string HostEmail, int Photos, long PhotoBytes, int QrCodes, int UsedQrCodes, int PublicGalleries, DateTimeOffset CreatedAt);
public sealed record AdminHostPage(IReadOnlyList<AdminHost> Items, int Page, int PageSize, int TotalCount);
public sealed record AdminHost(Guid Id, string Name, string Email, bool IsActive, bool IsBlocked, bool IsAdmin, int EventCount, DateTimeOffset CreatedAt);
public sealed record AdminEventPage(IReadOnlyList<AdminEvent> Items, int Page, int PageSize, int TotalCount);
public sealed record AdminEvent(Guid Id, string Name, string HostEmail, string Status, int PhotoCount, long PhotoBytes, int QrCodes, int UsedQrCodes, int PublicGalleryCount, DateTimeOffset CreatedAt);
public sealed record AdminAuditItem(Guid Id, string ActorType, string Action, string EntityType, Guid EntityId, string? Metadata, DateTimeOffset CreatedAt);
