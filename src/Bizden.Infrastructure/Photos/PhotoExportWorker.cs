using System.IO.Compression;
using Bizden.Application.Auditing;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Email;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bizden.Infrastructure.Photos;

public sealed class PhotoExportWorker(IServiceScopeFactory scopeFactory, ILogger<PhotoExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessOneAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Photo export worker iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOneAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BizdenDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var job = await db.PhotoExportJobs.Include(item => item.Event).OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(item => item.Status == PhotoExportStatus.Queued, cancellationToken);
        if (job is null) { await CleanupExpiredAsync(db, storage, cancellationToken); return; }
        job.Status = PhotoExportStatus.Processing;
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"bizden-export-{job.Id:N}.zip");
        try
        {
            var photos = await db.Photos.AsNoTracking().Where(photo => photo.EventId == job.EventId && photo.Status == PhotoStatus.Uploaded && photo.DeletedAt == null).OrderBy(photo => photo.UploadedAt).Select(photo => new { photo.StorageKey, photo.OriginalFileName }).ToListAsync(cancellationToken);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, useAsync: true))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var photo in photos)
                {
                    var bytes = await storage.DownloadAsync(photo.StorageKey, cancellationToken);
                    if (bytes is null) continue;
                    var baseName = Path.GetFileName(photo.OriginalFileName);
                    var name = baseName; var suffix = 2;
                    while (!usedNames.Add(name)) name = $"{Path.GetFileNameWithoutExtension(baseName)}-{suffix++}{Path.GetExtension(baseName)}";
                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(bytes, cancellationToken);
                }
            }
            var safeName = string.Concat(job.Event.Name.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
            job.FileName = $"bizden-{(string.IsNullOrWhiteSpace(safeName) ? "photos" : safeName)}.zip";
            job.StorageKey = $"exports/{job.Id:N}/{job.FileName}";
            await using var input = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, useAsync: true);
            if (!await storage.UploadAsync(job.StorageKey, "application/zip", input, cancellationToken)) throw new IOException("Export could not be stored.");
            job.Status = PhotoExportStatus.Ready;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
            await db.SaveChangesAsync(cancellationToken);
            await audit.RecordAsync(job.RequestedById, "Host", "PhotosExported", "Event", job.EventId, $"jobId={job.Id};photoCount={photos.Count}", cancellationToken);
            var ownerEmail = await db.HostUsers.Where(user => user.Id == job.Event.OwnerId).Select(user => user.Email).SingleAsync(cancellationToken);
            await email.SendExportReadyAsync(ownerEmail, job.Event.Name, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Photo export {ExportJobId} failed", job.Id);
            job.Status = PhotoExportStatus.Failed;
            job.Error = "Export could not be prepared. Please try again.";
            job.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task CleanupExpiredAsync(BizdenDbContext db, IObjectStorage storage, CancellationToken cancellationToken)
    {
        var jobs = await db.PhotoExportJobs.Where(job => job.Status == PhotoExportStatus.Ready && job.ExpiresAt < DateTimeOffset.UtcNow).Take(20).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            if (job.StorageKey is not null) await storage.DeleteAsync(job.StorageKey, cancellationToken);
            job.Status = PhotoExportStatus.Expired;
        }
        if (jobs.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
