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
            TeamDTOPublic? team = await _teamService.CreateAsync(newTeam);

            if (team is null)
                return BadRequest("Team invalid data.");

            return CreatedAtAction(nameof(GetTeam), new { id = team!.Id }, team);
        }

        // PATCH: api/Team/5 (partial update)
        [HttpPatch("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> PatchTeam(Guid id, [FromBody] TeamUpdateDTO dto)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            bool isAdmin = await _teamService.IsAdminAsync(userId, id);
            if (!isAdmin)
                return NotFound();

            TeamDTOPublic? teamUpdated = await _teamService.UpdateNameAsync(id, dto.Name);
            if (teamUpdated is null)
                return BadRequest(new { Message = "Invalid data." });

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

            bool isAdmin = await _teamService.IsAdminAsync(userId, id);
            if (!isAdmin)
                return NotFound();

            await _teamService.DeleteAsync(id);
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


            bool isAdmin = await _teamService.IsAdminAsync(currentUserId, id);
            if (!isAdmin)
                return NotFound();

            bool success = await _teamService.AddMemberAsync(id, dto.UserId);
            if (!success)
                return BadRequest(new { Message = "Cannot add member. User not found or already a member." });

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

            bool isAdmin = await _teamService.IsAdminAsync(currentUserId, id);
            if (!isAdmin)
                return NotFound();

            if (userId == currentUserId)
                return BadRequest(new { Message = "You cannot remove yourself. Use leave or transfer ownership." });

            bool success = await _teamService.RemoveMemberAsync(id, userId);
            if (!success)
                return BadRequest(new { Message = "Cannot remove member. User not found or is the last admin." });

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

            bool isAdmin = await _teamService.IsAdminAsync(currentUserId, id);
            if (!isAdmin)
                return NotFound();

            bool success = await _teamService.PromoteToAdminAsync(id, dto.UserId);
            if (!success)
                return BadRequest(new { Message = "Cannot promote. User not found or already admin." });

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

            //
            bool isAdmin = await _teamService.IsAdminAsync(currentUserId, id);
            if (!isAdmin)
                return NotFound();

            if (userId == currentUserId)
                return BadRequest(new { Message = "You cannot demote yourself." });

            bool success = await _teamService.DemoteFromAdminAsync(id, userId);
            if (!success)
                return BadRequest(new { Message = "Cannot demote. User not found, not admin, or is the last admin." });

            return NoContent();
        }
    }

    public record MemberActionDTO(string UserId);
}
