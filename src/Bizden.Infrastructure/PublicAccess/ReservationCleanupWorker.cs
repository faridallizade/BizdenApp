using Bizden.Application.PublicAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bizden.Infrastructure.PublicAccess;
public sealed class ReservationCleanupWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<IPublicQrService>().ExpireReservationsAsync(stoppingToken); }
    }
}
