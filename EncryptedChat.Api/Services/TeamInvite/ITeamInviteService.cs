using EncryptedChat.Models;

namespace EncryptedChat.Services;

public enum InviteJoinOutcome { Ok, Invalid, NoPublicKey, AlreadyMember }

public sealed record InviteJoinResult(InviteJoinOutcome Outcome, TeamDTOPublic? Team);

public interface ITeamInviteService
{
    Task<TeamInviteDTO?> CreateAsync(Guid teamId, string actorUserId, CancellationToken ct);
    Task<List<TeamInviteListItemDTO>?> ListAsync(Guid teamId, string actorUserId, CancellationToken ct);
    Task<bool> RevokeAsync(Guid teamId, Guid inviteId, string actorUserId, CancellationToken ct);
    Task<InvitePreviewDTO?> PreviewAsync(string token, CancellationToken ct);
    Task<InviteJoinResult> JoinAsync(string token, string userId, CancellationToken ct);
}
