using Bizden.Application.Photos;
using Bizden.Domain.Entities;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Photos;

public sealed class PhotoExportService(BizdenDbContext db, IObjectStorage storage) : IPhotoExportService
{
    public async Task<PhotoExportJobSummary?> QueueAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(@event => @event.Id == eventId && @event.OwnerId == ownerId, cancellationToken)) return null;
        var active = await db.PhotoExportJobs.AsNoTracking().Where(job => job.EventId == eventId && (job.Status == PhotoExportStatus.Queued || job.Status == PhotoExportStatus.Processing)).OrderByDescending(job => job.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (active is not null) return await ToSummaryAsync(active, cancellationToken);
        var job = new PhotoExportJob { Id = Guid.NewGuid(), EventId = eventId, RequestedById = ownerId, Status = PhotoExportStatus.Queued, CreatedAt = DateTimeOffset.UtcNow };
        db.PhotoExportJobs.Add(job);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            var competingJob = await db.PhotoExportJobs.AsNoTracking().Where(item => item.EventId == eventId && (item.Status == PhotoExportStatus.Queued || item.Status == PhotoExportStatus.Processing)).OrderByDescending(item => item.CreatedAt).FirstAsync(cancellationToken);
            return await ToSummaryAsync(competingJob, cancellationToken);
        }
        return await ToSummaryAsync(job, cancellationToken);
    }

    public async Task<IReadOnlyList<PhotoExportJobSummary>?> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(@event => @event.Id == eventId && @event.OwnerId == ownerId, cancellationToken)) return null;
        var jobs = await db.PhotoExportJobs.AsNoTracking().Where(job => job.EventId == eventId).OrderByDescending(job => job.CreatedAt).Take(20).ToListAsync(cancellationToken);
        return await Task.WhenAll(jobs.Select(job => ToSummaryAsync(job, cancellationToken)));
    }

    private async Task<PhotoExportJobSummary> ToSummaryAsync(PhotoExportJob job, CancellationToken cancellationToken)
    {
        var url = job.Status == PhotoExportStatus.Ready && job.StorageKey is not null ? await storage.PresignGetAsync(job.StorageKey, "application/zip", cancellationToken) : null;
        return new PhotoExportJobSummary(job.Id, job.Status.ToString(), job.FileName, url, job.Error, job.CreatedAt, job.CompletedAt, job.ExpiresAt);
    }
}
