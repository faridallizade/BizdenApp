using System.Globalization;
using System.Text;
using Bizden.Application.Events;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Events;

public sealed class HostEventService(BizdenDbContext dbContext) : IHostEventService
{
    public async Task<IReadOnlyList<HostEventSummary>> ListAsync(Guid ownerId, CancellationToken cancellationToken) => await dbContext.Events
        .AsNoTracking().Where(@event => @event.OwnerId == ownerId).OrderByDescending(@event => @event.EventDate)
        .Select(@event => new HostEventSummary(@event.Id, @event.Name, @event.EventDate, @event.TimeZone, @event.Status, @event.Invitations.Count))
        .ToListAsync(cancellationToken);

    public async Task<HostEventDetails?> GetAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events.AsNoTracking().Include(item => item.Invitations)
            .SingleOrDefaultAsync(item => item.Id == eventId && item.OwnerId == ownerId, cancellationToken);
        return @event is null ? null : ToDetails(@event);
    }

    public async Task<HostEventDetails> CreateAsync(Guid ownerId, CreateHostEventCommand command, CancellationToken cancellationToken)
    {
        Validate(command.Name, command.TimeZone, command.UploadStartAt, command.UploadEndAt);
        var @event = new Event
        {
            Id = Guid.NewGuid(), PublicId = Guid.NewGuid(), OwnerId = ownerId, Name = command.Name.Trim(), Description = CleanDescription(command.Description),
            Slug = await CreateSlugAsync(command.Name, cancellationToken), EventDate = command.EventDate, TimeZone = command.TimeZone.Trim(),
            UploadStartAt = command.UploadStartAt, UploadEndAt = command.UploadEndAt, Status = command.Status, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetails(@event);
    }

    public async Task<HostEventDetails?> UpdateAsync(Guid ownerId, Guid eventId, UpdateHostEventCommand command, CancellationToken cancellationToken)
    {
        Validate(command.Name, command.TimeZone, command.UploadStartAt, command.UploadEndAt);
        var @event = await dbContext.Events.Include(item => item.Invitations)
            .SingleOrDefaultAsync(item => item.Id == eventId && item.OwnerId == ownerId, cancellationToken);
        if (@event is null) return null;
        @event.Name = command.Name.Trim(); @event.Description = CleanDescription(command.Description); @event.EventDate = command.EventDate;
        @event.TimeZone = command.TimeZone.Trim(); @event.UploadStartAt = command.UploadStartAt; @event.UploadEndAt = command.UploadEndAt;
        @event.Status = command.Status; @event.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetails(@event);
    }

    private async Task<string> CreateSlugAsync(string name, CancellationToken cancellationToken)
    {
        var normalized = string.Concat(name.Trim().Normalize(NormalizationForm.FormD).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        var slug = new string(normalized.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        slug = string.IsNullOrWhiteSpace(slug) ? "tedbir" : slug[..Math.Min(slug.Length, 150)];
        var candidate = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 180)];
        while (await dbContext.Events.AnyAsync(item => item.Slug == candidate, cancellationToken)) candidate = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 9, 180)];
        return candidate;
    }

    private static void Validate(string name, string timeZone, DateTimeOffset start, DateTimeOffset end)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 160) throw new ArgumentException("Event name must be between 1 and 160 characters.");
        if (string.IsNullOrWhiteSpace(timeZone) || timeZone.Trim().Length > 64) throw new ArgumentException("A valid timezone is required.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim()); } catch (TimeZoneNotFoundException) { throw new ArgumentException("A valid IANA timezone is required."); }
        if (start > end) throw new ArgumentException("Upload start time must be before the end time.");
    }

    private static string? CleanDescription(string? description) => string.IsNullOrWhiteSpace(description) ? null : description.Trim()[..Math.Min(description.Trim().Length, 2_000)];
    private static HostEventDetails ToDetails(Event @event) => new(@event.Id, @event.PublicId, @event.Name, @event.Slug, @event.Description, @event.EventDate, @event.TimeZone, @event.UploadStartAt, @event.UploadEndAt, @event.Status, @event.Invitations.Count);
}
