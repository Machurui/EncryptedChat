using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        UserService _service;

        public UserController(UserService service)
        {
            _service = service;
        }

        // GET: api/User api/User/5 api/User/email@example.com
        [HttpGet]
        [Authorize(Roles = "Manager")]
        public ActionResult<IEnumerable<UserDTOPublic>> GetUsers([FromQuery] string? id, [FromQuery] string? email)
        {
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(email) && User.IsInRole("Admin") )
                return Ok(_service.GetAll());
            else if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(email) && !User.IsInRole("Admin"))
                return Unauthorized();

            var user = _service.Search(id, email);
            if (user == null)
                return NotFound();

            return Ok(new[] { user });
        }

        //GET: api/User/5/messages
        [HttpGet("{id}/messages")]
        [Authorize(Roles = "User")]
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

        // PUT: api/User/5
        [HttpPut("{id}")]
        public IActionResult PutUser(string id, UserDTO user)
        {
            var userToUpdate = _service.GetById(id);

            if (userToUpdate is not null)
            {
                _service.Update(id, user);
                return NoContent();
            }
            else
                return NotFound();
        }

        // DELETE: api/User/5
        [HttpDelete("{id}")]
        public IActionResult DeleteUser(string id)
        {
            var userToDelete = _service.GetById(id);

            if (userToDelete is not null)
            {
                _service.Delete(id);
                return NoContent();
            }
            else
                return NotFound();
        }
    }
}
