using Bizden.Domain.Entities;
using Bizden.Domain.Enums;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Bizden.Infrastructure.Photos;

public sealed class PhotoProcessingWorker(IServiceScopeFactory scopes, ILogger<PhotoProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await ProcessAsync(stoppingToken);
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BizdenDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var photos = await db.Photos
            .Where(x => x.Status == PhotoStatus.Uploaded && x.MediaProcessedAt == null)
            .OrderBy(x => x.UploadedAt)
            .Take(10)
            .ToListAsync(ct);

        foreach (var photo in photos)
        {
            try
            {
                var source = await storage.DownloadAsync(photo.StorageKey, ct)
                    ?? throw new InvalidOperationException("Source object is unavailable.");
                using var image = Image.Load(source);
                image.Mutate(x => x.AutoOrient());
                image.Metadata.ExifProfile = null;

                photo.Width = image.Width;
                photo.Height = image.Height;
                var previewKey = $"derivatives/{photo.Id:N}/preview.jpg";
                var thumbnailKey = $"derivatives/{photo.Id:N}/thumbnail.jpg";
                using var preview = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(1600, 1600) }));
                using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(480, 480) }));

                if (!await storage.UploadAsync(previewKey, "image/jpeg", Encode(preview, 85), ct)
                    || !await storage.UploadAsync(thumbnailKey, "image/jpeg", Encode(thumbnail, 78), ct))
                    throw new InvalidOperationException("Derivative upload failed.");

                photo.PreviewStorageKey = previewKey;
                photo.ThumbnailStorageKey = thumbnailKey;
                photo.ProcessingError = null;
                photo.MediaProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (UnknownImageFormatException exception)
            {
                Quarantine(photo, exception);
            }
            catch (InvalidImageContentException exception)
            {
                Quarantine(photo, exception);
            }
            catch (Exception exception)
            {
                photo.ProcessingError = exception.GetType().Name;
                logger.LogWarning(exception, "Photo {PhotoId} processing will be retried", photo.Id);
            }
        }

        if (photos.Count > 0) await db.SaveChangesAsync(ct);
    }

    private void Quarantine(Photo photo, Exception exception)
    {
        photo.Status = PhotoStatus.Quarantined;
        photo.ProcessingError = exception.GetType().Name;
        photo.MediaProcessedAt = DateTimeOffset.UtcNow;
        logger.LogWarning(exception, "Photo {PhotoId} quarantined because it could not be decoded", photo.Id);
    }

    private static byte[] Encode(Image image, int quality)
    {
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder { Quality = quality });
        return stream.ToArray();
    }
}
