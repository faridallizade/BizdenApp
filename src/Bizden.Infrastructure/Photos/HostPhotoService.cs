using Bizden.Application.Photos;
using Bizden.Application.Auditing;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Photos;

public sealed class HostPhotoService(BizdenDbContext db, IObjectStorage storage, IAuditLogService audit) : IHostPhotoService
{
    public async Task<HostPhotoPage?> ListAsync(Guid ownerId, Guid eventId, Guid? invitationId, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        if (!await db.Events.AsNoTracking().AnyAsync(x => x.Id == eventId && x.OwnerId == ownerId, ct)) return null;
        var query = db.Photos.AsNoTracking().Where(x => x.EventId == eventId && x.Status == PhotoStatus.Uploaded && x.DeletedAt == null);
        if (invitationId is not null) query = query.Where(x => x.InvitationId == invitationId);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.UploadedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.InvitationId, Label = x.Invitation.Label, x.OriginalFileName, x.MimeType, x.FileSize, x.UploadedAt, x.StorageKey }).ToListAsync(ct);
        var items = await Task.WhenAll(rows.Select(async x => new HostPhotoItem(x.Id, x.InvitationId, x.Label, x.OriginalFileName, x.MimeType, x.FileSize, x.UploadedAt!.Value, await storage.PresignGetAsync(x.StorageKey, x.MimeType, ct))));
        return new HostPhotoPage(items, page, pageSize, total);
    }

    public async Task<HostPhotoDownload?> GetDownloadAsync(Guid ownerId, Guid photoId, CancellationToken ct)
    {
        var photo = await db.Photos.AsNoTracking().Include(x => x.Event).SingleOrDefaultAsync(x => x.Id == photoId && x.Event.OwnerId == ownerId && x.Status == PhotoStatus.Uploaded && x.DeletedAt == null, ct);
        if (photo is null) return null;
        var url = await storage.PresignGetAsync(photo.StorageKey, photo.MimeType, ct);
        return url is null ? null : new HostPhotoDownload(photo.OriginalFileName, photo.MimeType, url);
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid photoId, CancellationToken ct)
    {
        var photo = await db.Photos.Include(x => x.Event).SingleOrDefaultAsync(x => x.Id == photoId && x.Event.OwnerId == ownerId && x.Status == PhotoStatus.Uploaded && x.DeletedAt == null, ct);
        if (photo is null) return false;
        photo.Status = PhotoStatus.Deleted; photo.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(ownerId, "Host", "PhotoDeleted", "Photo", photo.Id, $"eventId={photo.EventId}", ct);
        return true;
    }

    public async Task DeleteStoredObjectsAsync(CancellationToken ct)
    {
        var photos = await db.Photos.Where(x => x.Status == PhotoStatus.Deleted && x.StorageDeletedAt == null).OrderBy(x => x.DeletedAt).Take(50).ToListAsync(ct);
        foreach (var photo in photos)
            if (await storage.DeleteAsync(photo.StorageKey, ct)) photo.StorageDeletedAt = DateTimeOffset.UtcNow;
        if (photos.Count > 0) await db.SaveChangesAsync(ct);
    }
}
