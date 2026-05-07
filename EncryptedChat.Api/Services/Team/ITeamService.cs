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

        Task<TeamDTOPublic?> CreateAsync(TeamDTO team);

        Task<TeamDTOPublic?> UpdateAsync(Guid id, TeamDTO team);

        Task<TeamDTOPublic?> UpdateNameAsync(Guid id, string name);

        Task<TeamDTOPublic?> DeleteAsync(Guid id);

        Task<bool> IsAdminAsync(string userId, Guid teamId);

        Task<bool> IsMemberAsync(string userId, Guid teamId);

        Task<bool> AddMemberAsync(Guid teamId, string userId);

        Task<bool> RemoveMemberAsync(Guid teamId, string userId);

        Task<bool> PromoteToAdminAsync(Guid teamId, string userId);

        Task<bool> DemoteFromAdminAsync(Guid teamId, string userId);
    }
}
