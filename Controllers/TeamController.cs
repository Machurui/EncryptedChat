using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using System.Threading.Tasks;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TeamController : ControllerBase
    {
        TeamService _service;

        public TeamController(TeamService service)
        {
            _service = service;
        }

        // GET: api/Team
        [HttpGet]
        public IEnumerable<TeamDTOPublic> GetTeams()
        {
            return _service.GetAll();
        }

        // GET: api/Team/5
        [HttpGet("{id}")]
        public ActionResult<TeamDTOPublic> GetTeam(int id)
        {
            var team = _service.GetById(id);

            if (team is not null)
            {
                return team;
            }
            else
            {
                return NotFound();
            }
        }

        // POST: api/Team
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public IActionResult PostTeam(TeamDTO newTeam)
        {
            var team = _service.CreateAsync(newTeam);

            if (team is null)
            {
                return BadRequest("Team invalid data.");
            }

            return CreatedAtAction(nameof(GetTeam), new { id = team!.Id }, team);
        }

        // PUT: api/Team/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTeam(int id, TeamDTO team)
        {
            var teamToUpdate = _service.GetById(id);

            if (teamToUpdate is not null)
            {
                await _service.UpdateAsync(id, team);
                return NoContent();
            }
            else
            {

                return NotFound();
            }
        }

        // DELETE: api/Team/5
        [HttpDelete("{id}")]
        public IActionResult DeleteTeam(int id)
        {
            var teamToDelete = _service.GetById(id);

            if (teamToDelete is not null)
            {
                _service.Delete(id);
                return NoContent();
            }
            else
            {
                return NotFound();
            }
        }
    }
}
