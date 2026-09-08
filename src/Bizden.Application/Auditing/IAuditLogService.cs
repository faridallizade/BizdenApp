namespace Bizden.Application.Auditing;

public interface IAuditLogService
{
    Task RecordAsync(Guid? actorId, string actorType, string action, string entityType, Guid entityId, string? metadata, CancellationToken cancellationToken);
}
