using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;

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

        // GET: api/User
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IEnumerable<UserDTOPublic> GetUsers()
        {
            return _service.GetAll();
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public ActionResult<UserDTOPublic> GetUser(string id)
        {
            var user = _service.GetById(id);

            if (user is not null)
            {
                return user;
            }
            else
            {
                return NotFound();
            }
        }

        // PUT: api/User/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
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
            {
                return NotFound();
            }
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
            {
                return NotFound();
            }
        }
    }
}
