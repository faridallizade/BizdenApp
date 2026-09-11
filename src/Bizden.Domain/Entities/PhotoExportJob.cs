using Bizden.Domain.Enums;

namespace Bizden.Domain.Entities;

public sealed class PhotoExportJob
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid RequestedById { get; set; }
    public PhotoExportStatus Status { get; set; }
    public string? StorageKey { get; set; }
    public string? FileName { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public Event Event { get; set; } = null!;
}
