namespace Bizden.Application.Invitations;

public interface IInvitationManagementService
{
    Task<IReadOnlyList<InvitationSummary>> ListAsync(Guid ownerId, Guid eventId, CancellationToken cancellationToken);
    Task<InvitationBatchResult?> CreateAsync(Guid ownerId, CreateInvitationBatchCommand command, CancellationToken cancellationToken);
    Task<InvitationSummary?> UpdateAsync(Guid ownerId, Guid eventId, Guid invitationId, UpdateInvitationCommand command, CancellationToken cancellationToken);
    Task<InvitationTokenResult?> RegenerateAsync(Guid ownerId, Guid eventId, Guid invitationId, CancellationToken cancellationToken);
}

public sealed record CreateInvitationBatchCommand(Guid EventId, string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, int Count);
public sealed record UpdateInvitationCommand(string? Label, int UploadLimit, DateTimeOffset? ExpiresAt, bool IsActive);
public sealed record InvitationSummary(Guid Id, string? Label, int UploadLimit, int ReservedUploads, int CompletedUploads, bool IsActive, DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt);
public sealed record InvitationTokenResult(InvitationSummary Invitation, string Token);
public sealed record InvitationBatchResult(IReadOnlyList<InvitationTokenResult> Invitations);
