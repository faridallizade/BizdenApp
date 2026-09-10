using Bizden.Application.Administration;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Administration;

public sealed class AdminService(BizdenDbContext db) : IAdminService
{
    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var hosts = await db.HostUsers.AsNoTracking().ToListAsync(cancellationToken);
        var events = await db.Events.IgnoreQueryFilters().AsNoTracking().Where(@event => @event.DeletedAt == null).ToListAsync(cancellationToken);
        var photos = await db.Photos.AsNoTracking().Where(photo => photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null).ToListAsync(cancellationToken);
        var invitations = await db.Invitations.AsNoTracking().ToListAsync(cancellationToken);
        var publicGalleries = await db.SharedGalleries.AsNoTracking().Where(gallery => gallery.DeletedAt == null).ToListAsync(cancellationToken);
        var monthly = events.GroupBy(@event => new DateTime(@event.CreatedAt.UtcDateTime.Year, @event.CreatedAt.UtcDateTime.Month, 1))
            .OrderBy(group => group.Key).Select(group => new AdminMonthlyMetric(group.Key.ToString("yyyy-MM"), group.Count(), photos.Count(photo => group.Select(@event => @event.Id).Contains(photo.EventId)), invitations.Count(invitation => group.Select(@event => @event.Id).Contains(invitation.EventId)), invitations.Count(invitation => invitation.LastUsedAt != null && group.Select(@event => @event.Id).Contains(invitation.EventId)), photos.Where(photo => group.Select(@event => @event.Id).Contains(photo.EventId)).Sum(photo => photo.FileSize))).ToList();
        var eventMetrics = events.OrderByDescending(@event => @event.CreatedAt).Take(100).Select(@event => new AdminEventMetric(@event.Id, @event.Name, hosts.FirstOrDefault(host => host.Id == @event.OwnerId)?.Email ?? "", photos.Count(photo => photo.EventId == @event.Id), photos.Where(photo => photo.EventId == @event.Id).Sum(photo => photo.FileSize), invitations.Count(invitation => invitation.EventId == @event.Id), invitations.Count(invitation => invitation.EventId == @event.Id && invitation.LastUsedAt != null), publicGalleries.Count(gallery => gallery.EventId == @event.Id), @event.CreatedAt)).ToList();
        return new AdminDashboard(hosts.Count, hosts.Count(host => host.IsActive && host.BlockedAt is null), events.Count, photos.Count, photos.Sum(photo => photo.FileSize), invitations.Count, invitations.Count(invitation => invitation.LastUsedAt != null), publicGalleries.Count, monthly, eventMetrics);
    }

    public async Task<AdminHostPage> ListHostsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.HostUsers.AsNoTracking().OrderByDescending(host => host.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var hosts = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(host => new AdminHost(host.Id, host.Name, host.Email, host.IsActive, host.BlockedAt != null, host.IsAdmin, db.Events.IgnoreQueryFilters().Count(@event => @event.OwnerId == host.Id && @event.DeletedAt == null), host.CreatedAt)).ToListAsync(cancellationToken);
        return new AdminHostPage(hosts, page, pageSize, total);
    }

    public async Task<AdminHost?> UpdateHostAsync(Guid hostId, UpdateAdminHostCommand command, CancellationToken cancellationToken)
    {
        var host = await db.HostUsers.SingleOrDefaultAsync(user => user.Id == hostId, cancellationToken);
        if (host is null) return null;
        host.IsActive = command.IsActive;
        host.BlockedAt = command.IsBlocked ? DateTimeOffset.UtcNow : null;
        host.BlockedReason = command.IsBlocked ? command.BlockedReason?.Trim() : null;
        host.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var count = await db.Events.IgnoreQueryFilters().CountAsync(@event => @event.OwnerId == host.Id && @event.DeletedAt == null, cancellationToken);
        return new AdminHost(host.Id, host.Name, host.Email, host.IsActive, host.BlockedAt != null, host.IsAdmin, count, host.CreatedAt);
    }

    public async Task<AdminEventPage> ListEventsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Events.IgnoreQueryFilters().AsNoTracking().Where(@event => @event.DeletedAt == null).OrderByDescending(@event => @event.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var events = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(@event => new AdminEvent(@event.Id, @event.Name, db.HostUsers.Where(host => host.Id == @event.OwnerId).Select(host => host.Email).FirstOrDefault() ?? "", @event.Status.ToString(), db.Photos.Count(photo => photo.EventId == @event.Id && photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null), db.Photos.Where(photo => photo.EventId == @event.Id && photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null).Sum(photo => (long?)photo.FileSize) ?? 0, db.Invitations.Count(invitation => invitation.EventId == @event.Id), db.Invitations.Count(invitation => invitation.EventId == @event.Id && invitation.LastUsedAt != null), db.SharedGalleries.Count(gallery => gallery.EventId == @event.Id && gallery.DeletedAt == null), @event.CreatedAt)).ToListAsync(cancellationToken);
        return new AdminEventPage(events, page, pageSize, total);
    }

    public async Task<bool> SoftDeleteEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await db.Events.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == eventId && item.DeletedAt == null, cancellationToken);
        if (@event is null) return false;
        @event.DeletedAt = @event.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AdminAuditItem>> ListAuditAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        return await db.AuditLogs.AsNoTracking().OrderByDescending(log => log.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(log => new AdminAuditItem(log.Id, log.ActorType, log.Action, log.EntityType, log.EntityId, log.Metadata, log.CreatedAt)).ToListAsync(cancellationToken);
    }
}
