using System.Security.Cryptography;
using System.Text;
using Bizden.Application.Invitations;
using Bizden.Domain.Entities;
using Bizden.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bizden.Infrastructure.Invitations;

public sealed class InvitationManagementService(BizdenDbContext dbContext) : IInvitationManagementService
{
    public async Task<IReadOnlyList<InvitationSummary>> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(ownerId, eventId, cancellationToken)) return [];
        return await dbContext.Invitations.AsNoTracking().Where(item => item.EventId == eventId).OrderByDescending(item => item.CreatedAt)
            .Select(item => new InvitationSummary(item.Id, item.Label, item.UploadLimit, item.ReservedUploads, item.CompletedUploads, item.IsActive, item.ExpiresAt, item.CreatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<InvitationBatchResult?> CreateAsync(Guid ownerId, CreateInvitationBatchCommand command, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(ownerId, command.EventId, cancellationToken)) return null;
        Validate(command.Label, command.UploadLimit, command.ExpiresAt, command.Count);
        var now = DateTimeOffset.UtcNow;
        var tokens = new List<InvitationTokenResult>(command.Count);
        for (var index = 0; index < command.Count; index++)
        {
            var token = NewToken();
            var invitation = new Invitation { Id = Guid.NewGuid(), EventId = command.EventId, TokenHash = Hash(token), Label = LabelFor(command.Label, index, command.Count), UploadLimit = command.UploadLimit, IsActive = true, ExpiresAt = command.ExpiresAt, CreatedAt = now };
            dbContext.Invitations.Add(invitation);
            tokens.Add(new InvitationTokenResult(ToSummary(invitation), token));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return new InvitationBatchResult(tokens);
    }

    public async Task<InvitationSummary?> UpdateAsync(Guid ownerId, Guid eventId, Guid invitationId, UpdateInvitationCommand command, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(ownerId, eventId, cancellationToken)) return null;
        Validate(command.Label, command.UploadLimit, command.ExpiresAt, 1);
        var invitation = await dbContext.Invitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.EventId == eventId, cancellationToken);
        if (invitation is null || command.UploadLimit < invitation.ReservedUploads + invitation.CompletedUploads) throw new ArgumentException("New limit cannot be lower than already used uploads.");
        invitation.Label = CleanLabel(command.Label); invitation.UploadLimit = command.UploadLimit; invitation.ExpiresAt = command.ExpiresAt; invitation.IsActive = command.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToSummary(invitation);
    }

    public async Task<InvitationTokenResult?> RegenerateAsync(Guid ownerId, Guid eventId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(ownerId, eventId, cancellationToken)) return null;
        var oldInvitation = await dbContext.Invitations.SingleOrDefaultAsync(item => item.Id == invitationId && item.EventId == eventId, cancellationToken);
        if (oldInvitation is null) return null;
        oldInvitation.IsActive = false;
        var token = NewToken();
        var replacement = new Invitation { Id = Guid.NewGuid(), EventId = eventId, TokenHash = Hash(token), Label = oldInvitation.Label, UploadLimit = oldInvitation.UploadLimit, IsActive = true, ExpiresAt = oldInvitation.ExpiresAt, CreatedAt = DateTimeOffset.UtcNow };
        dbContext.Invitations.Add(replacement);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new InvitationTokenResult(ToSummary(replacement), token);
    }

    private Task<bool> OwnsEventAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken) => dbContext.Events.AnyAsync(item => item.Id == eventId && item.OwnerId == ownerId, cancellationToken);
    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static string? CleanLabel(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim()[..Math.Min(label.Trim().Length, 120)];
    private static string? LabelFor(string? label, int index, int count) => count == 1 ? CleanLabel(label) : $"{CleanLabel(label) ?? "QR"} {index + 1}";
    private static InvitationSummary ToSummary(Invitation item) => new(item.Id, item.Label, item.UploadLimit, item.ReservedUploads, item.CompletedUploads, item.IsActive, item.ExpiresAt, item.CreatedAt);
    private static void Validate(string? label, int limit, DateTimeOffset? expiresAt, int count)
    {
        if (label?.Trim().Length > 120) throw new ArgumentException("QR label must be 120 characters or fewer.");
        if (limit is < 1 or > 10_000) throw new ArgumentException("Photo limit must be between 1 and 10,000.");
        if (count is < 1 or > 50) throw new ArgumentException("QR batch size must be between 1 and 50.");
        if (expiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow) throw new ArgumentException("QR expiry must be in the future.");
    }
}
