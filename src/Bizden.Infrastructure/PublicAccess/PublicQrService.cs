using System.Security.Cryptography;
using System.Text;
using Bizden.Application.PublicAccess;
using Bizden.Domain.Entities;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.PublicAccess;

public sealed class PublicQrService(BizdenDbContext db, IObjectStorage storage) : IPublicQrService
{
    public async Task<PublicQrView> GetAsync(string token, CancellationToken ct)
    {
        var invitation = await db.Invitations.AsNoTracking().Include(x => x.Event).SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct);
        return invitation is null ? Unavailable("NOT_FOUND") : View(invitation, DateTimeOffset.UtcNow);
    }

    public async Task<ReservationResult> ReserveAsync(string token, ReserveUploadCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 128 || command.FileSize is < 1 or > 26_214_400 || string.IsNullOrWhiteSpace(command.FileName) || command.FileName.Length > 255 || command.MimeType is not ("image/jpeg" or "image/png" or "image/webp" or "image/heic")) return new("INVALID_REQUEST", null, null, 0);
        var invitation = await db.Invitations.AsNoTracking().Include(x => x.Event).SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct);
        if (invitation is null) return new("NOT_FOUND", null, null, 0);
        var view = View(invitation, DateTimeOffset.UtcNow);
        if (view.State != "READY") return new(view.State, null, null, view.RemainingPhotos);
        var existing = await db.UploadReservations.AsNoTracking().SingleOrDefaultAsync(x => x.InvitationId == invitation.Id && x.IdempotencyKey == command.IdempotencyKey, ct);
        if (existing is not null) return new(existing.Status.ToString().ToUpperInvariant(), existing.Id, existing.ExpiresAt, view.RemainingPhotos);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var reserved = await db.Invitations.Where(x => x.Id == invitation.Id && x.IsActive && x.ReservedUploads + x.CompletedUploads < x.UploadLimit)
            .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.ReservedUploads, x => x.ReservedUploads + 1), ct);
        if (reserved == 0) { await transaction.RollbackAsync(ct); return new("LIMIT_REACHED", null, null, 0); }
        var now = DateTimeOffset.UtcNow;
        var photo = new Photo { Id = Guid.NewGuid(), EventId = invitation.EventId, InvitationId = invitation.Id, StorageKey = $"pending/{Guid.NewGuid():N}", OriginalFileName = command.FileName.Trim(), MimeType = command.MimeType.Trim()[..Math.Min(command.MimeType.Trim().Length, 127)], FileSize = command.FileSize, Status = PhotoStatus.PendingUpload, CreatedAt = now };
        var reservation = new UploadReservation { Id = Guid.NewGuid(), InvitationId = invitation.Id, PhotoId = photo.Id, Status = ReservationStatus.Reserved, IdempotencyKey = command.IdempotencyKey, ExpiresAt = now.AddMinutes(15), CreatedAt = now };
        db.AddRange(photo, reservation); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new("RESERVED", reservation.Id, reservation.ExpiresAt, Math.Max(0, view.RemainingPhotos - 1));
    }

    public async Task CancelAsync(string token, Guid reservationId, CancellationToken ct)
    {
        var invitation = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct); if (invitation is null) return;
        var reservation = await db.UploadReservations.SingleOrDefaultAsync(x => x.Id == reservationId && x.InvitationId == invitation.Id && x.Status == ReservationStatus.Reserved, ct); if (reservation is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct); reservation.Status = ReservationStatus.Cancelled;
        await db.Invitations.Where(x => x.Id == invitation.Id && x.ReservedUploads > 0).ExecuteUpdateAsync(x => x.SetProperty(y => y.ReservedUploads, y => y.ReservedUploads - 1), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<UploadUrlResult> PrepareUploadAsync(string token, Guid reservationId, CancellationToken ct)
    {
        var invitation = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct); if (invitation is null) return new("NOT_FOUND", null, null);
        var reservation = await db.UploadReservations.AsNoTracking().Include(x => x.Photo).SingleOrDefaultAsync(x => x.Id == reservationId && x.InvitationId == invitation.Id && x.Status == ReservationStatus.Reserved && x.ExpiresAt > DateTimeOffset.UtcNow, ct);
        if (reservation is null) return new("RESERVATION_UNAVAILABLE", null, null);
        var url = await storage.PresignPutAsync(reservation.Photo.StorageKey, reservation.Photo.MimeType, ct); return url is null ? new("STORAGE_UNAVAILABLE", null, null) : new("READY", url, DateTimeOffset.UtcNow.AddMinutes(10));
    }

    public async Task<ReservationResult> CompleteUploadAsync(string token, Guid reservationId, CancellationToken ct)
    {
        var invitation = await db.Invitations.AsNoTracking().Include(x => x.Event).SingleOrDefaultAsync(x => x.TokenHash == Hash(token), ct); if (invitation is null) return new("NOT_FOUND", null, null, 0);
        var reservation = await db.UploadReservations.Include(x => x.Photo).SingleOrDefaultAsync(x => x.Id == reservationId && x.InvitationId == invitation.Id, ct);
        if (reservation is null || reservation.Status != ReservationStatus.Reserved) return new("RESERVATION_UNAVAILABLE", null, null, 0);
        if (!await storage.VerifyAsync(reservation.Photo.StorageKey, reservation.Photo.FileSize, reservation.Photo.MimeType, ct)) return new("UPLOAD_NOT_VERIFIED", reservation.Id, reservation.ExpiresAt, 0);
        await using var tx = await db.Database.BeginTransactionAsync(ct); reservation.Status = ReservationStatus.Completed; reservation.CompletedAt = DateTimeOffset.UtcNow; reservation.Photo.Status = PhotoStatus.Uploaded; reservation.Photo.UploadedAt = DateTimeOffset.UtcNow;
        await db.Invitations.Where(x => x.Id == invitation.Id && x.ReservedUploads > 0).ExecuteUpdateAsync(x => x.SetProperty(y => y.ReservedUploads, y => y.ReservedUploads - 1).SetProperty(y => y.CompletedUploads, y => y.CompletedUploads + 1), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new("COMPLETED", reservation.Id, null, Math.Max(0, invitation.UploadLimit - invitation.ReservedUploads - invitation.CompletedUploads));
    }

    public async Task ExpireReservationsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow; var expired = await db.UploadReservations.Where(x => x.Status == ReservationStatus.Reserved && x.ExpiresAt <= now).ToListAsync(ct);
        if (expired.Count == 0) return; await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var group in expired.GroupBy(x => x.InvitationId)) { foreach (var item in group) item.Status = ReservationStatus.Expired; var count = group.Count(); await db.Invitations.Where(x => x.Id == group.Key).ExecuteUpdateAsync(x => x.SetProperty(y => y.ReservedUploads, y => y.ReservedUploads >= count ? y.ReservedUploads - count : 0), ct); }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private static PublicQrView View(Invitation i, DateTimeOffset now)
    {
        var state = !i.IsActive ? "INACTIVE" : i.ExpiresAt <= now ? "EXPIRED" : i.Event.Status != EventStatus.Active ? "EVENT_UNAVAILABLE" : now < i.Event.UploadStartAt ? "NOT_OPEN" : now > i.Event.UploadEndAt ? "WINDOW_CLOSED" : i.ReservedUploads + i.CompletedUploads >= i.UploadLimit ? "LIMIT_REACHED" : "READY";
        return state == "NOT_FOUND" ? Unavailable(state) : new(state, i.Event.Name, i.Event.Description, i.Event.EventDate, i.Event.TimeZone, Math.Max(0, i.UploadLimit - i.ReservedUploads - i.CompletedUploads), i.UploadLimit, i.Event.UploadEndAt);
    }
    private static PublicQrView Unavailable(string state) => new(state, null, null, null, null, 0, null, null);
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
