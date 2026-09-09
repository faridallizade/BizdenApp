namespace Bizden.Application.Galleries;

public interface IHostGalleryService
{
    Task<HostGalleryShare?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<HostGalleryShare?> EnableAsync(Guid ownerId, Guid eventId, string pin, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
}

public sealed record HostGalleryShare(Guid PublicId, DateTimeOffset EnabledAt);
