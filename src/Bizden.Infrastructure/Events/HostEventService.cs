using System.Globalization;
using System.Text;
using Bizden.Application.Events;
using Bizden.Application.Auditing;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Bizden.Infrastructure.PublicAccess;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Events;

public sealed class HostEventService(BizdenDbContext dbContext, IAuditLogService audit, IObjectStorage storage) : IHostEventService
{
    public async Task<IReadOnlyList<HostEventSummary>> ListAsync(Guid ownerId, CancellationToken cancellationToken) => await dbContext.Events
        .AsNoTracking().Where(@event => @event.OwnerId == ownerId).OrderByDescending(@event => @event.EventDate)
        .Select(@event => new HostEventSummary(@event.Id, @event.Name, @event.EventDate, @event.TimeZone, @event.Status, @event.Invitations.Count, @event.BrandColor, @event.CustomMessage))
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
            UploadStartAt = command.UploadStartAt, UploadEndAt = command.UploadEndAt, Status = command.Status, BrandColor = CleanColor(command.BrandColor), CustomMessage = CleanMessage(command.CustomMessage), CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Events.Add(@event);
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(ownerId, "Host", "EventCreated", "Event", @event.Id, $"status={@event.Status}", cancellationToken);
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
        @event.Status = command.Status; @event.BrandColor = CleanColor(command.BrandColor); @event.CustomMessage = CleanMessage(command.CustomMessage); @event.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(ownerId, "Host", "EventUpdated", "Event", @event.Id, $"status={@event.Status}", cancellationToken);
        return ToDetails(@event);
    }

    public async Task<CoverUpload?> CreateCoverUploadAsync(Guid ownerId, Guid eventId, string fileName, string mimeType, long fileSize, CancellationToken ct)
    {
        if (fileSize is < 1 or > 10_485_760 || mimeType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Cover image must be a JPEG, PNG, or WEBP up to 10 MB.");
        if (!await dbContext.Events.AsNoTracking().AnyAsync(x => x.Id == eventId && x.OwnerId == ownerId, ct)) return null;
        var key = $"covers/{eventId:N}/{Guid.NewGuid():N}";
        var url = await storage.PresignPutAsync(key, mimeType, ct);
        return url is null ? null : new CoverUpload(key, url);
    }

    public async Task<HostEventDetails?> CompleteCoverUploadAsync(Guid ownerId, Guid eventId, string key, long fileSize, string mimeType, CancellationToken ct)
    {
        if (!key.StartsWith($"covers/{eventId:N}/", StringComparison.Ordinal) || fileSize is < 1 or > 10_485_760 || mimeType is not ("image/jpeg" or "image/png" or "image/webp")) throw new ArgumentException("Invalid cover upload.");
        var @event = await dbContext.Events.Include(x => x.Invitations).SingleOrDefaultAsync(x => x.Id == eventId && x.OwnerId == ownerId, ct);
        if (@event is null) return null;
        if (!await storage.VerifyAsync(key, fileSize, mimeType, ct)) throw new ArgumentException("Cover image could not be verified.");
        var previousKey = @event.CoverImageKey; @event.CoverImageKey = key; @event.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(previousKey)) await storage.DeleteAsync(previousKey, ct);
        await audit.RecordAsync(ownerId, "Host", "EventCoverUpdated", "Event", eventId, null, ct);
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
    private static string? CleanColor(string? color) => string.IsNullOrWhiteSpace(color) ? null : System.Text.RegularExpressions.Regex.IsMatch(color.Trim(), "^#[0-9a-fA-F]{6}$") ? color.Trim() : throw new ArgumentException("Brand color must be a hex value such as #805742.");
    private static string? CleanMessage(string? message) => string.IsNullOrWhiteSpace(message) ? null : message.Trim()[..Math.Min(message.Trim().Length, 500)];
    private static HostEventDetails ToDetails(Event @event) => new(@event.Id, @event.PublicId, @event.Name, @event.Slug, @event.Description, @event.EventDate, @event.TimeZone, @event.UploadStartAt, @event.UploadEndAt, @event.Status, @event.Invitations.Count, @event.BrandColor, @event.CustomMessage);
}
