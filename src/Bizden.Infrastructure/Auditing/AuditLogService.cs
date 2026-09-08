using Bizden.Application.Auditing;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Bizden.Infrastructure.Auditing;

public sealed class AuditLogService(BizdenDbContext db, ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task RecordAsync(Guid? actorId, string actorType, string action, string entityType, Guid entityId, string? metadata, CancellationToken ct)
    {
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(), ActorId = actorId, ActorType = actorType[..Math.Min(actorType.Length, 32)], Action = action[..Math.Min(action.Length, 64)],
                EntityType = entityType[..Math.Min(entityType.Length, 64)], EntityId = entityId, Metadata = metadata?[..Math.Min(metadata.Length, 1_000)], CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Audit record failed for {Action} {EntityType} {EntityId}", action, entityType, entityId);
        }
    }
}
