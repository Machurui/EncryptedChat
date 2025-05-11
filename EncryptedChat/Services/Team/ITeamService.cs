using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Services
{
    public interface ITeamService
    {
        Task<IEnumerable<TeamDTOPublic?>?> GetAllAsync();

        Task<TeamDTOPublic?> GetByIdAsync(int id);

        Task<TeamDTOPublic?> CreateAsync(TeamDTO team);

        Task<TeamDTOPublic?> UpdateAsync(int id, TeamDTO team);

        Task<TeamDTOPublic?> DeleteAsync(int id);

        Task<bool> IsAdminAsync(string userId, int teamId);
    }
}