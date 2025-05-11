using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        // GET: api/Team
        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IEnumerable<TeamDTOPublic?>?> GetTeams()
        {
            return await _teamService.GetAllAsync();
        }

        // GET: api/Team/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<TeamDTOPublic?>?> GetTeam(int id)
        {
            var team = await _teamService.GetByIdAsync(id);

            if (team is null)
                return NotFound();

            return team;
        }

        // POST: api/Team
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult?> PostTeam(TeamDTO newTeam)
        {
            var team = await _teamService.CreateAsync(newTeam);

            if (team is null)
                return BadRequest("Team invalid data.");

            return CreatedAtAction(nameof(GetTeam), new { id = team!.Id }, team);
        }

        // PUT: api/Team/5
        [HttpPut("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult?> PutTeam(int id, string userId, TeamDTO team)
        {
            var isAdmin = await _teamService.IsAdminAsync(userId, id);
            if (!isAdmin)
                return Unauthorized();

            var teamToUpdate = await _teamService.GetByIdAsync(id);
            if (teamToUpdate is null)
                return NotFound();

            var teamUpdated = await _teamService.UpdateAsync(id, team);
            if (teamUpdated is null)
                return BadRequest("Team invalid data.");

            return NoContent();
        }

        // DELETE: api/Team/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult?> DeleteTeam(int id, string userId)
        {
            var isAdmin = await _teamService.IsAdminAsync(userId, id);
            if (!isAdmin)
                return Unauthorized();

            var teamToDelete = await _teamService.GetByIdAsync(id);

            if (teamToDelete is null)
                return NotFound();

            await _teamService.DeleteAsync(id);
            return NoContent();
        }
    }
}
