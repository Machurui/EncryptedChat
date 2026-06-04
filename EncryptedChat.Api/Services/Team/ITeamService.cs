using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services
{
    public interface ITeamService
    {
        Task<IEnumerable<TeamDTOPublic?>?> GetAllAsync();

        Task<TeamDTOPublic?> GetByIdAsync(Guid id);

        Task<TeamDTOPublic?> CreateAsync(TeamDTO team, string creatorId);

        Task<TeamDTOPublic?> UpdateAsync(Guid id, TeamDTO team, string actorId);

        Task<TeamDTOPublic?> UpdateNameAsync(Guid id, string name, string actorId);

        Task<TeamDTOPublic?> UpdatePartialAsync(Guid id, TeamUpdateDTO dto, string actorId);

        Task<TeamDTOPublic?> DeleteAsync(Guid id, string actorId);

        Task<bool> IsAdminAsync(string userId, Guid teamId);

        Task<bool> IsOwnerAsync(string userId, Guid teamId);

        Task<bool> IsMemberAsync(string userId, Guid teamId);

        // Sets the caller's read marker for the team to now. Returns the
        // timestamp written, or null if the user is not a member.
        Task<DateTime?> MarkReadAsync(string userId, Guid teamId);

        // Atomic ownership transfer. fromUserId must currently be Owner;
        // toUserId must already be a member of the team. Returns false on
        // any precondition violation. On success: fromUserId becomes Admin,
        // toUserId becomes Owner, both within a single SaveChanges call.
        Task<bool> TransferOwnershipAsync(Guid teamId, string fromUserId, string toUserId);

        Task<bool> AddMemberAsync(Guid teamId, string userId, string actorId);

        Task<bool> RemoveMemberAsync(Guid teamId, string userId, string actorId);

        Task<bool> PromoteToAdminAsync(Guid teamId, string userId, string actorId);

        Task<bool> DemoteFromAdminAsync(Guid teamId, string userId, string actorId);

        Task<IReadOnlyList<string>> GetMemberUserIdsAsync(Guid teamId);

        Task<TeamDTOPublic?> GetOrCreateDirectMessageAsync(string userId, string friendId, string? myWrappedKey = null, string? friendWrappedKey = null);

        Task<(TeamDTOPublic? Dm, bool IsNew)> GetOrCreateDirectMessageWithStatusAsync(string userId, string friendId, string? myWrappedKey = null, string? friendWrappedKey = null);

        Task<TeamDTOPublic?> GetTeamByUrlTokenAsync(string token, string userId);
    }
}
