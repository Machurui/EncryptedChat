using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TeamController(ITeamService teamService) : ControllerBase
    {
        private readonly ITeamService _teamService = teamService;

        private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET: api/Team
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<TeamDTOPublic?>?>> GetTeams()
        {
            IEnumerable<TeamDTOPublic?>? teams = await _teamService.GetAllAsync();

            if (teams == null || !teams.Any())
                return NotFound();

            return Ok(teams);
        }

        // GET: api/Team/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<TeamDTOPublic?>?> GetTeam(Guid id)
        {
            TeamDTOPublic? team = await _teamService.GetByIdAsync(id);

            if (team is null || team.Id != id)
                return NotFound();

            return team;
        }

        // POST: api/Team
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult?> PostTeam([FromBody] TeamDTO newTeam)
        {
            string? creatorId = GetCurrentUserId();
            if (string.IsNullOrEmpty(creatorId))
                return Unauthorized();

            TeamDTOPublic? team = await _teamService.CreateAsync(newTeam, creatorId);

            if (team is null)
                return BadRequest(new { Message = "Données invalides" });

            return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, team);
        }

        // PATCH: api/Team/5 (partial update)
        [HttpPatch("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> PatchTeam(Guid id, [FromBody] TeamUpdateDTO dto)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            TeamDTOPublic? teamUpdated = await _teamService.UpdateNameAsync(id, dto.Name, userId);
            if (teamUpdated is null)
                return NotFound();

            return NoContent();
        }

        // DELETE: api/Team/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            TeamDTOPublic? deleted = await _teamService.DeleteAsync(id, userId);
            if (deleted is null)
                return NotFound();

            return NoContent();
        }

        // ==================== MEMBER MANAGEMENT ====================

        // POST: api/Team/{id}/members
        [HttpPost("{id}/members")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddMember(Guid id, [FromBody] MemberActionDTO dto)
        {
            string? currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            bool success = await _teamService.AddMemberAsync(id, dto.UserId, currentUserId);
            if (!success)
                return NotFound();

            return NoContent();
        }

        // DELETE: api/Team/{id}/members/{userId}
        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> RemoveMember(Guid id, string userId)
        {
            string? currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            if (userId == currentUserId)
                return BadRequest(new { Message = "You cannot remove yourself. Use leave or transfer ownership." });

            bool success = await _teamService.RemoveMemberAsync(id, userId, currentUserId);
            if (!success)
                return NotFound();

            return NoContent();
        }

        // POST: api/Team/{id}/admins
        [HttpPost("{id}/admins")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> PromoteToAdmin(Guid id, [FromBody] MemberActionDTO dto)
        {
            string? currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            bool success = await _teamService.PromoteToAdminAsync(id, dto.UserId, currentUserId);
            if (!success)
                return NotFound();

            return NoContent();
        }

        // DELETE: api/Team/{id}/admins/{userId}
        [HttpDelete("{id}/admins/{userId}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DemoteFromAdmin(Guid id, string userId)
        {
            string? currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            if (userId == currentUserId)
                return BadRequest(new { Message = "You cannot demote yourself." });

            bool success = await _teamService.DemoteFromAdminAsync(id, userId, currentUserId);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }

    public record MemberActionDTO(string UserId);
}
