using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EncryptedChat.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
    public class UserController : ControllerBase
    {
        private readonly UserService _service;

        public UserController(UserService service)
        {
            _service = service;
        }

        /// GET: api/User
        /// GET: api/User?id=5
        /// GET: api/User?email=email@example.com
        [HttpGet]
        public ActionResult<IEnumerable<UserDTOPublic>> GetUsers(
            [FromQuery] string? id,
            [FromQuery] string? email)
        {
            // No filters -> return all users to any "User"
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(email))
            {
                return Ok(_service.GetAll());
            }

            // Filter by id or email
            var user = _service.Search(id, email);
            if (user == null)
                return NotFound();

            return Ok(new[] { user });
        }

        /// GET: api/User/{id}/messages
        [HttpGet("{id}/messages")]
        public ActionResult<IEnumerable<MessageDTO>> GetMessages(string id)
        {
            var user = _service.GetById(id);
            if (user == null)
                return NotFound();

            var messages = _service.GetUserMessages(id);
            if (messages == null)
                return NotFound();

            return Ok(messages);
        }

        /// GET: api/User/{id}/teams
        [HttpGet("{id}/teams")]
        public async Task<ActionResult<IEnumerable<TeamDTOPublic>>> GetTeams(string id)
        {
            var user = _service.GetById(id);
            if (user == null)
                return NotFound();

            var teams = await _service.GetUserTeamsAsync(id);
            if (teams == null || teams.Count == 0)
                return NotFound();

            return Ok(teams);
        }

        /// PUT: api/User/{id}
        [HttpPut("{id}")]
        public IActionResult PutUser(string id, UserDTO user)
        {
            var userToUpdate = _service.GetById(id);

            if (userToUpdate is not null)
            {
                _service.Update(id, user);
                return NoContent();
            }

            return NotFound();
        }

        /// DELETE: api/User/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteUser(string id)
        {
            var userToDelete = _service.GetById(id);

            if (userToDelete is not null)
            {
                _service.Delete(id);
                return NoContent();
            }

            return NotFound();
        }
    }
}