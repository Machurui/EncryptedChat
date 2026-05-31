using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using EncryptedChat.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TeamController(
        ITeamService teamService,
        IHubContext<ChatHub> hubContext,
        ITeamKeyShareService teamKeyShares) : ControllerBase
    {
        private readonly ITeamService _teamService = teamService;
        private readonly IHubContext<ChatHub> _hubContext = hubContext;
        private readonly ITeamKeyShareService _teamKeyShares = teamKeyShares;

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

        // GET: api/Team/by-token/{token} - Resolve URL token to team for the current user
        [HttpGet("by-token/{token}")]
        public async Task<IActionResult> GetTeamByToken(string token)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();

            var team = await _teamService.GetTeamByUrlTokenAsync(token, userId);
            if (team == null)
                return NotFound();

            return Ok(team);
        }

        // GET: api/Team/5/details - For team members
        [HttpGet("{id}/details")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<TeamDTOPublic?>> GetTeamDetails(Guid id)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            bool isMember = await _teamService.IsMemberAsync(userId, id);
            if (!isMember)
                return Forbid();

            TeamDTOPublic? team = await _teamService.GetByIdAsync(id);
            if (team is null)
                return NotFound();

            return Ok(team);
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

            var memberIds = await _teamService.GetMemberUserIdsAsync(team.Id);
            if (memberIds.Count > 0)
            {
                await _hubContext.Clients.Users(memberIds).SendAsync("TeamCreated", team);
            }

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

            TeamDTOPublic? teamUpdated = await _teamService.UpdatePartialAsync(id, dto, userId);
            if (teamUpdated is null)
                return NotFound();

            var memberIds = await _teamService.GetMemberUserIdsAsync(id);
            if (memberIds.Count > 0)
            {
                await _hubContext.Clients.Users(memberIds).SendAsync("TeamUpdated", teamUpdated);
            }

            return Ok(teamUpdated);
        }

        // DELETE: api/Team/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DeleteTeam(Guid id)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var memberIds = await _teamService.GetMemberUserIdsAsync(id);

            TeamDTOPublic? deleted = await _teamService.DeleteAsync(id, userId);
            if (deleted is null)
                return NotFound();

            if (memberIds.Count > 0)
            {
                await _hubContext.Clients.Users(memberIds).SendAsync("TeamDeleted", new { TeamId = id });
            }

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

            var team = await _teamService.GetByIdAsync(id);
            if (team != null)
            {
                var memberIds = await _teamService.GetMemberUserIdsAsync(id);
                if (memberIds.Count > 0)
                {
                    await _hubContext.Clients.Users(memberIds).SendAsync("TeamMemberAdded", new { TeamId = id, UserId = dto.UserId, Team = team });
                }
            }

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

            var memberIdsBefore = await _teamService.GetMemberUserIdsAsync(id);

            bool success = await _teamService.RemoveMemberAsync(id, userId, currentUserId);
            if (!success)
                return NotFound();

            if (memberIdsBefore.Count > 0)
            {
                await _hubContext.Clients.Users(memberIdsBefore).SendAsync("TeamMemberRemoved", new { TeamId = id, UserId = userId });
            }

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

        // POST: api/Team/dm/{friendId} - Get or create a DM with a friend
        [HttpPost("dm/{friendId}")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<TeamDTOPublic>> GetOrCreateDirectMessage(string friendId)
        {
            string? currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var (dm, _) = await _teamService.GetOrCreateDirectMessageWithStatusAsync(currentUserId, friendId);
            if (dm == null)
                return BadRequest(new { Message = "Could not create direct message channel." });

            // Friend is intentionally NOT notified here. ChatHub.SendMessageToTeam
            // detects the first message in a DM and sends DirectMessageCreated +
            // the initial ReceiveMessage to the friend at that point.
            return Ok(dm);
        }

        [HttpGet("{teamId}/key-shares")]
        public async Task<IActionResult> GetMyKeyShares(Guid teamId)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            List<TeamKeyShareDTO> shares = await _teamKeyShares.GetMineForTeamAsync(userId, teamId);
            return Ok(shares);
        }

        [HttpPost("{teamId}/members/{memberId}/key-share")]
        public async Task<IActionResult> AddMemberKeyShare(
            Guid teamId, string memberId, [FromBody] AddMemberKeyShareDTO dto)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            KeyShareInsertResult result = await _teamKeyShares.InsertKeyShareForMemberAsync(
                userId, teamId, memberId, dto.WrappedKey);
            return result switch
            {
                KeyShareInsertResult.Ok => NoContent(),
                KeyShareInsertResult.Forbidden => Forbid(),
                KeyShareInsertResult.AlreadyExists => Conflict(new { Message = "Key share already provisioned" }),
                KeyShareInsertResult.NotFound => NotFound(),
                _ => BadRequest()
            };
        }

        [HttpPost("{teamId}/members/{memberId}/remove")]
        public async Task<IActionResult> RemoveMember(
            Guid teamId, string memberId, [FromBody] RemoveMemberDTO dto)
        {
            string? userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            RemoveAndRotateResult result = await _teamKeyShares.RemoveMemberAndRotateAsync(
                userId, teamId, memberId, dto.NewKeyShares);
            return result switch
            {
                RemoveAndRotateResult.Ok => NoContent(),
                RemoveAndRotateResult.Forbidden => Forbid(),
                RemoveAndRotateResult.NotFound => NotFound(),
                RemoveAndRotateResult.CannotRemoveLastAdmin => BadRequest(new { Message = "Cannot remove the last admin" }),
                RemoveAndRotateResult.KeyShareCoverageMismatch => BadRequest(new { Message = "NewKeyShares must cover exactly the remaining members" }),
                _ => BadRequest()
            };
        }
    }

    public record MemberActionDTO(string UserId);
}
