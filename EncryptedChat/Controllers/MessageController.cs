using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using EncryptedChat.Services;
using System.Threading.Tasks;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        MessageService _service;

        public MessageController(MessageService service)
        {
            _service = service;
        }

        // GET: api/Message/
        [HttpGet]
        public async Task<IEnumerable<MessageDTOPublic>> GetMessages()
        {
            return await _service.GetAllAsync() ?? [];
        }

        // GET: api/Message/team/7
        [HttpGet("team/{teamId}")]
        public async Task<ActionResult<IEnumerable<MessageDTOPublic>>> GetMessageByTeam(int teamId)
        {
            var messages = await _service.GetAllByTeamAsync(teamId);

            if (messages is not null)
            {
                return Ok(messages);
            }
            else
            {
                return NotFound();
            }
        }

        // GET: api/Message/5
        [HttpGet("{id}")]
        public async Task<ActionResult<MessageDTOPublic>> GetMessage(int id)
        {
            var message = await _service.GetByIdAsync(id);

            if (message is not null)
            {
                return message;
            }
            else
            {
                return NotFound();
            }
        }

        // POST: api/Message
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<IActionResult> PostMessage(MessageDTO newMessage)
        {
            var message = await _service.CreateAsync(newMessage);

            if (message is null)
            {
                return BadRequest("Message invalid data or the user is not in the team.");
            }

            return CreatedAtAction(nameof(GetMessage), new { id = message!.Id }, message);
        }

        // PUT: api/Message/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMessage(int id, MessageDTO message)
        {
            var messageToUpdate = await _service.GetByIdAsync(id);

            if (messageToUpdate is not null)
            {
                var messageUpdated = await _service.UpdateAsync(id, message);
                if (messageUpdated is null)
                    return BadRequest("Message invalid data.");

                return NoContent();
            }
            else
            {

                return NotFound();
            }
        }

        // DELETE: api/Message/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var messageToDelete = await _service.GetByIdAsync(id);

            if (messageToDelete is not null)
            {
                await _service.DeleteAsync(id);
                return NoContent();
            }
            else
            {
                return NotFound();
            }
        }
    }
}
