using Bizden.Domain.Enums;

namespace Bizden.Application.Events;

public interface IHostEventService
{
    Task<IReadOnlyList<HostEventSummary>> ListAsync(Guid ownerId, CancellationToken cancellationToken);
    Task<HostEventDetails?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<HostEventDetails> CreateAsync(Guid ownerId, CreateHostEventCommand command, CancellationToken cancellationToken);
    Task<HostEventDetails?> UpdateAsync(Guid ownerId, Guid eventId, UpdateHostEventCommand command, CancellationToken cancellationToken);
}

public sealed record CreateHostEventCommand(
    string Name,
    string? Description,
    DateTimeOffset EventDate,
    string TimeZone,
    DateTimeOffset UploadStartAt,
    DateTimeOffset UploadEndAt,
    EventStatus Status);

public sealed record UpdateHostEventCommand(
    string Name,
    string? Description,
    DateTimeOffset EventDate,
    string TimeZone,
    DateTimeOffset UploadStartAt,
    DateTimeOffset UploadEndAt,
    EventStatus Status);

public sealed record HostEventSummary(Guid Id, string Name, DateTimeOffset EventDate, string TimeZone, EventStatus Status, int InvitationCount);

public sealed record HostEventDetails(
    Guid Id,
    Guid PublicId,
    string Name,
    string Slug,
    string? Description,
    DateTimeOffset EventDate,
    string TimeZone,
    DateTimeOffset UploadStartAt,
    DateTimeOffset UploadEndAt,
    EventStatus Status,
    int InvitationCount);
