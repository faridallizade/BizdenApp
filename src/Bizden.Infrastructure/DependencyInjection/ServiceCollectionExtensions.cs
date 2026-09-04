using Bizden.Application.Authentication;
using Bizden.Application.Events;
using Bizden.Application.Invitations;
using Bizden.Application.PublicAccess;
using Bizden.Infrastructure.Authentication;
using Bizden.Infrastructure.Events;
using Bizden.Infrastructure.Invitations;
using Bizden.Infrastructure.PublicAccess;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bizden.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is required.");

        services.AddDbContext<BizdenDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IHostAuthenticationService, HostAuthenticationService>();
        services.AddScoped<IHostEventService, HostEventService>();
        services.AddScoped<IInvitationManagementService, InvitationManagementService>();
        services.AddScoped<IPublicQrService, PublicQrService>();
        services.AddSingleton<IObjectStorage, R2ObjectStorage>();
        services.AddHostedService<ReservationCleanupWorker>();

        return services;
    }
}
