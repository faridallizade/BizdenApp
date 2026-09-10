namespace Bizden.Application.Galleries;

public interface IHostGalleryService
{
    Task<IReadOnlyList<HostGalleryShare>> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<HostGalleryShare?> CreateAsync(Guid ownerId, CreateGalleryShareCommand command, CancellationToken cancellationToken);
    Task<HostGalleryShare?> ReplacePhotosAsync(Guid ownerId, Guid eventId, Guid galleryId, IReadOnlyCollection<Guid> photoIds, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid ownerId, Guid eventId, Guid galleryId, CancellationToken cancellationToken);
    Task<HostGalleryShare?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<HostGalleryShare?> EnableAsync(Guid ownerId, Guid eventId, string pin, CancellationToken cancellationToken);
    Task<bool> DisableAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
}

public sealed record CreateGalleryShareCommand(Guid EventId, string Name, string Pin, IReadOnlyCollection<Guid> PhotoIds, bool AllMatching = false, Guid? InvitationId = null);
public sealed record HostGalleryShare(Guid Id, Guid PublicId, string Name, DateTimeOffset EnabledAt, int PhotoCount);
