using Bizden.Application.Authentication;
using Bizden.Application.Auditing;
using Bizden.Application.Events;
using Bizden.Application.Invitations;
using Bizden.Application.Galleries;
using Bizden.Application.PublicAccess;
using Bizden.Application.Photos;
using Bizden.Infrastructure.Authentication;
using Bizden.Infrastructure.Email;
using Bizden.Infrastructure.Auditing;
using Bizden.Infrastructure.Events;
using Bizden.Infrastructure.Invitations;
using Bizden.Infrastructure.Galleries;
using Bizden.Infrastructure.PublicAccess;
using Bizden.Infrastructure.Photos;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.Observability;
using Bizden.Infrastructure.Administration;
using Bizden.Application.Administration;
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
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IHostEventService, HostEventService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IInvitationManagementService, InvitationManagementService>();
        services.AddScoped<IPublicQrService, PublicQrService>();
        services.AddScoped<IHostPhotoService, HostPhotoService>();
        services.AddScoped<IPhotoExportService, PhotoExportService>();
        services.AddScoped<IHostGalleryService, GalleryService>();
        services.AddScoped<IPublicGalleryService, GalleryService>();
        services.AddSingleton<RuntimeMetrics>();
        services.AddSingleton<IObjectStorage, R2ObjectStorage>();
        services.AddHostedService<ReservationCleanupWorker>();
        services.AddHostedService<DeletedPhotoCleanupWorker>();
        services.AddHostedService<PhotoProcessingWorker>();
        services.AddHostedService<PhotoExportWorker>();

        return services;
    }
}
