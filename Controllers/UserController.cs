using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using EncryptedChat.Services;

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
        public IEnumerable<UserDTOPublic> GetUsers()
        {
            return _service.GetAll();
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public ActionResult<UserDTOPublic> GetUser(int id)
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

        // POST: api/User
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public IActionResult PostUser(UserDTO newUser)
        {
            var user = _service.Create(newUser);

            if (user is null)
            {
                return BadRequest("User already exists or invalid data.");
            }

            return CreatedAtAction(nameof(GetUser), new { id = user!.Id }, user);
        }

        // PUT: api/User/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public IActionResult PutUser(int id, UserDTO user)
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
        public IActionResult DeleteUser(int id)
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
