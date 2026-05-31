using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface ITeamKeyShareService
{
    // For the calling user: returns every TeamKeyShare row they own for the team
    // across all generations they have access to.
    Task<List<TeamKeyShareDTO>> GetMineForTeamAsync(string userId, Guid teamId);

    // Admin operation: wrap a key share for a newly-added member at the current
    // generation. Returns Forbidden if caller is not admin, NotFound if team or
    // member missing, AlreadyExists (409) if a share for this (team, member, gen)
    // already exists (idempotency on admin race).
    Task<KeyShareInsertResult> InsertKeyShareForMemberAsync(
        string adminUserId, Guid teamId, string memberId, string wrappedKey);

    // Admin operation: atomic remove + rotate. Validates that NewKeyShares
    // covers exactly the remaining members. Deletes the removed member's row +
    // all their TeamKeyShare rows; bumps Team.KeyGeneration; inserts new
    // TeamKeyShare rows at the new generation.
    Task<RemoveAndRotateResult> RemoveMemberAndRotateAsync(
        string adminUserId, Guid teamId, string removedMemberId,
        IReadOnlyList<KeyShareEntryDTO> newKeyShares);
}

public enum KeyShareInsertResult { Ok, Forbidden, AlreadyExists, NotFound }
public enum RemoveAndRotateResult { Ok, Forbidden, KeyShareCoverageMismatch, NotFound, CannotRemoveLastAdmin }
