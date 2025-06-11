using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly IMessageService _messageService;

        public MessageController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        // GET: api/Message/
        [HttpGet]
        [Authorize(Roles = "Manager")]
        public async Task<IEnumerable<MessageDTOPublic>> GetMessages()
        {
            return await _messageService.GetAllAsync() ?? [];
        }

        // GET: api/Message/team/{teamId}
        [HttpGet("team/{teamId}")]
        [Authorize(Roles = "User")]
        public async Task<ActionResult<IEnumerable<MessageDTOPublic>>> GetMessageByTeam(int teamId)
        {
            var messages = await _messageService.GetAllByTeamAsync(teamId);

            if (messages is not null)
                return Ok(messages);
            else
                return NotFound();
        }

        // GET: api/Message/5
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<MessageDTOPublic>> GetMessage(int id)
        {
            var message = await _messageService.GetByIdAsync(id);

            if (message is not null)
                return message;
            else
                return NotFound();
        }

        // PUT: api/Message/5
        [HttpPut("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> PutMessage(int id, MessageDTO message)
        {
            var messageToUpdate = await _messageService.GetByIdAsync(id);

            if (messageToUpdate is not null)
            {
                var messageUpdated = await _messageService.UpdateAsync(id, message);
                if (messageUpdated is null)
                    return BadRequest("Message invalid data.");

                return NoContent();
            }
            else
                return NotFound();
        }

        // DELETE: api/Message/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var messageToDelete = await _messageService.GetByIdAsync(id);

            if (messageToDelete is not null)
            {
                await _messageService.DeleteAsync(id);
                return NoContent();
            }
            else
                return NotFound();
        }
    }
}
