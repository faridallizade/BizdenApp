using Bizden.Application.Photos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bizden.Infrastructure.Photos;

public sealed class DeletedPhotoCleanupWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IHostPhotoService>().DeleteStoredObjectsAsync(stoppingToken);
        }
    }
}
