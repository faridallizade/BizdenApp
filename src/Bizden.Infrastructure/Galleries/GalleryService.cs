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
    private readonly PasswordHasher<Event> pinHasher = new();

    public async Task<HostGalleryShare?> EnableAsync(Guid ownerId, Guid eventId, string pin, CancellationToken cancellationToken)
    {
        if (pin.Length is < 4 or > 12 || !pin.All(char.IsDigit)) throw new ArgumentException("PIN must contain 4 to 12 digits.");
        var @event = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.OwnerId == ownerId, cancellationToken);
        if (@event is null) return null;
        @event.PublicId = Guid.NewGuid();
        @event.GalleryPinHash = pinHasher.HashPassword(@event, pin);
        @event.GalleryEnabledAt = DateTimeOffset.UtcNow;
        @event.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new HostGalleryShare(@event.PublicId, @event.GalleryEnabledAt.Value);
    }

    public async Task<bool> DisableAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.OwnerId == ownerId, cancellationToken);
        if (@event is null) return false;
        @event.GalleryPinHash = null;
        @event.GalleryEnabledAt = null;
        @event.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PublicGalleryInfo?> GetInfoAsync(Guid publicId, CancellationToken cancellationToken)
    {
        var @event = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.PublicId == publicId && x.GalleryEnabledAt != null && x.GalleryPinHash != null, cancellationToken);
        if (@event is null) return null;
        var coverUrl = @event.CoverImageKey is null ? null : await storage.PresignGetAsync(@event.CoverImageKey, "image/jpeg", cancellationToken);
        return new PublicGalleryInfo(@event.Name, @event.Description, @event.EventDate, @event.BrandColor, @event.CustomMessage, coverUrl);
    }

    public async Task<PublicGallery?> UnlockAsync(Guid publicId, string pin, CancellationToken cancellationToken)
    {
        var @event = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.PublicId == publicId && x.GalleryEnabledAt != null && x.GalleryPinHash != null, cancellationToken);
        if (@event is null || pinHasher.VerifyHashedPassword(@event, @event.GalleryPinHash!, pin) == PasswordVerificationResult.Failed) return null;
        var rows = await db.Photos.AsNoTracking().Where(x => x.EventId == @event.Id && x.Status == PhotoStatus.Uploaded && x.DeletedAt == null)
            .OrderByDescending(x => x.UploadedAt).Take(200).Select(x => new { x.Id, x.OriginalFileName, x.UploadedAt, x.ThumbnailStorageKey, x.PreviewStorageKey }).ToListAsync(cancellationToken);
        var photos = await Task.WhenAll(rows.Select(async x => new PublicGalleryPhoto(x.Id, x.OriginalFileName, x.UploadedAt!.Value,
            x.ThumbnailStorageKey is null ? null : await storage.PresignGetAsync(x.ThumbnailStorageKey, "image/jpeg", cancellationToken),
            x.PreviewStorageKey is null ? null : await storage.PresignGetAsync(x.PreviewStorageKey, "image/jpeg", cancellationToken))));
        var coverUrl = @event.CoverImageKey is null ? null : await storage.PresignGetAsync(@event.CoverImageKey, "image/jpeg", cancellationToken);
        return new PublicGallery(@event.Name, @event.Description, @event.EventDate, @event.BrandColor, @event.CustomMessage, coverUrl, photos);
    }
    public async Task<HostGalleryShare?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.OwnerId == ownerId, cancellationToken);
        return @event?.GalleryEnabledAt is { } enabled && !string.IsNullOrWhiteSpace(@event.GalleryPinHash)
            ? new HostGalleryShare(@event.PublicId, enabled) : null;
    }
}
