using Bizden.Application.Galleries;
using Bizden.Domain.Entities;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Galleries;

public sealed class GalleryService(BizdenDbContext db, IObjectStorage storage) : IHostGalleryService, IPublicGalleryService
{
    private readonly PasswordHasher<SharedGallery> pinHasher = new();

    public async Task<IReadOnlyList<HostGalleryShare>> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken) =>
        await db.SharedGalleries.AsNoTracking()
            .Where(gallery => gallery.EventId == eventId && gallery.Event.OwnerId == ownerId && gallery.DeletedAt == null)
            .OrderByDescending(gallery => gallery.CreatedAt)
            .Select(gallery => new HostGalleryShare(gallery.Id, gallery.PublicId, gallery.Name, gallery.EnabledAt, gallery.Photos.Count))
            .ToListAsync(cancellationToken);

    public async Task<HostGalleryShare?> CreateAsync(Guid ownerId, CreateGalleryShareCommand command, CancellationToken cancellationToken)
    {
        ValidatePin(command.Pin);
        var name = command.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Gallery name must contain 1 to 120 characters.");
        var @event = await db.Events.SingleOrDefaultAsync(x => x.Id == command.EventId && x.OwnerId == ownerId, cancellationToken);
        if (@event is null) return null;
        var photos = await GetEligiblePhotosAsync(command.EventId, command.PhotoIds, command.AllMatching, command.InvitationId, cancellationToken);
        if (!command.AllMatching && photos.Count != command.PhotoIds.Distinct().Count()) throw new ArgumentException("Every selected photo must belong to this event and be ready.");

        var now = DateTimeOffset.UtcNow;
        var gallery = new SharedGallery { Id = Guid.NewGuid(), EventId = @event.Id, PublicId = Guid.NewGuid(), Name = name, EnabledAt = now, CreatedAt = now, UpdatedAt = now };
        gallery.PinHash = pinHasher.HashPassword(gallery, command.Pin);
        foreach (var photo in photos) gallery.Photos.Add(new SharedGalleryPhoto { SharedGalleryId = gallery.Id, PhotoId = photo.Id, AddedAt = now });
        db.SharedGalleries.Add(gallery);
        await db.SaveChangesAsync(cancellationToken);
        return new HostGalleryShare(gallery.Id, gallery.PublicId, gallery.Name, gallery.EnabledAt, photos.Count);
    }

    public async Task<HostGalleryShare?> ReplacePhotosAsync(Guid ownerId, Guid eventId, Guid galleryId, IReadOnlyCollection<Guid> photoIds, CancellationToken cancellationToken)
    {
        var gallery = await db.SharedGalleries.Include(item => item.Photos).SingleOrDefaultAsync(item => item.Id == galleryId && item.EventId == eventId && item.Event.OwnerId == ownerId && item.DeletedAt == null, cancellationToken);
        if (gallery is null) return null;
        var photos = await GetEligiblePhotosAsync(eventId, photoIds, false, null, cancellationToken);
        if (photos.Count != photoIds.Distinct().Count()) throw new ArgumentException("Every selected photo must belong to this event and be ready.");
        gallery.Photos.Clear();
        var now = DateTimeOffset.UtcNow;
        foreach (var photo in photos) gallery.Photos.Add(new SharedGalleryPhoto { SharedGalleryId = gallery.Id, PhotoId = photo.Id, AddedAt = now });
        gallery.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return new HostGalleryShare(gallery.Id, gallery.PublicId, gallery.Name, gallery.EnabledAt, photos.Count);
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid eventId, Guid galleryId, CancellationToken cancellationToken)
    {
        var gallery = await db.SharedGalleries.SingleOrDefaultAsync(item => item.Id == galleryId && item.EventId == eventId && item.Event.OwnerId == ownerId && item.DeletedAt == null, cancellationToken);
        if (gallery is null) return false;
        gallery.DeletedAt = gallery.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Legacy wrappers keep the existing single-gallery UI working while it moves to the multi-gallery API.
    public async Task<HostGalleryShare?> EnableAsync(Guid ownerId, Guid eventId, string pin, CancellationToken cancellationToken)
    {
        var existing = await ListAsync(ownerId, eventId, cancellationToken);
        if (existing.FirstOrDefault() is { } share) return share;
        var photoIds = await db.Photos.Where(photo => photo.EventId == eventId && photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null).Select(photo => photo.Id).ToListAsync(cancellationToken);
        return await CreateAsync(ownerId, new CreateGalleryShareCommand(eventId, "Paylaşılan qalereya", pin, photoIds), cancellationToken);
    }

    public async Task<bool> DisableAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        var galleries = await db.SharedGalleries.Where(gallery => gallery.EventId == eventId && gallery.Event.OwnerId == ownerId && gallery.DeletedAt == null).ToListAsync(cancellationToken);
        if (galleries.Count == 0) return false;
        var now = DateTimeOffset.UtcNow;
        foreach (var gallery in galleries) gallery.DeletedAt = gallery.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<HostGalleryShare?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken) => (await ListAsync(ownerId, eventId, cancellationToken)).FirstOrDefault();

    public async Task<PublicGalleryInfo?> GetInfoAsync(Guid publicId, CancellationToken cancellationToken)
    {
        var gallery = await FindPublicGalleryAsync(publicId, cancellationToken);
        if (gallery is null) return null;
        var @event = gallery.Event;
        var coverUrl = @event.CoverImageKey is null ? null : await storage.PresignGetAsync(@event.CoverImageKey, "image/jpeg", cancellationToken);
        return new PublicGalleryInfo(@event.Name, @event.Description, @event.EventDate, @event.BrandColor, @event.CustomMessage, coverUrl);
    }

    public async Task<PublicGallery?> UnlockAsync(Guid publicId, string pin, CancellationToken cancellationToken)
    {
        var gallery = await FindPublicGalleryAsync(publicId, cancellationToken);
        if (gallery is null || pinHasher.VerifyHashedPassword(gallery, gallery.PinHash, pin) == PasswordVerificationResult.Failed) return null;
        var rows = await db.SharedGalleryPhotos.AsNoTracking()
            .Where(item => item.SharedGalleryId == gallery.Id && item.Photo.Status == PhotoStatus.Uploaded && item.Photo.DeletedAt == null)
            .OrderByDescending(item => item.Photo.UploadedAt).Take(500)
            .Select(item => new { item.Photo.Id, item.Photo.OriginalFileName, item.Photo.UploadedAt, item.Photo.ThumbnailStorageKey, item.Photo.PreviewStorageKey })
            .ToListAsync(cancellationToken);
        var photos = await Task.WhenAll(rows.Select(async item => new PublicGalleryPhoto(item.Id, item.OriginalFileName, item.UploadedAt!.Value,
            item.ThumbnailStorageKey is null ? null : await storage.PresignGetAsync(item.ThumbnailStorageKey, "image/jpeg", cancellationToken),
            item.PreviewStorageKey is null ? null : await storage.PresignGetAsync(item.PreviewStorageKey, "image/jpeg", cancellationToken))));
        var @event = gallery.Event;
        var coverUrl = @event.CoverImageKey is null ? null : await storage.PresignGetAsync(@event.CoverImageKey, "image/jpeg", cancellationToken);
        return new PublicGallery(@event.Name, @event.Description, @event.EventDate, @event.BrandColor, @event.CustomMessage, coverUrl, photos);
    }

    private async Task<SharedGallery?> FindPublicGalleryAsync(Guid publicId, CancellationToken cancellationToken) =>
        await db.SharedGalleries.AsNoTracking().Include(gallery => gallery.Event).SingleOrDefaultAsync(gallery => gallery.PublicId == publicId && gallery.DeletedAt == null, cancellationToken);

    private async Task<List<Photo>> GetEligiblePhotosAsync(Guid eventId, IReadOnlyCollection<Guid> photoIds, bool allMatching, Guid? invitationId, CancellationToken cancellationToken)
    {
        var ids = photoIds.Distinct().ToList();
        if (!allMatching && ids.Count == 0) return [];
        var query = db.Photos.Where(photo => photo.EventId == eventId && photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null);
        if (invitationId is not null) query = query.Where(photo => photo.InvitationId == invitationId);
        if (!allMatching) query = query.Where(photo => ids.Contains(photo.Id));
        return await query.ToListAsync(cancellationToken);
    }

    private static void ValidatePin(string pin)
    {
        if (pin.Length is < 4 or > 12 || !pin.All(char.IsDigit)) throw new ArgumentException("PIN must contain 4 to 12 digits.");
    }
}
