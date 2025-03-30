using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using EncryptedChat.Services;

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

        // GET: api/Message
        [HttpGet]
        public IEnumerable<Message> GetMessages()
        {
            throw new NotImplementedException();
        }

        // GET: api/Message/5
        [HttpGet("{id}")]
        public ActionResult<Message> GetMessage(int id)
        {
            throw new NotImplementedException();
        }

        // POST: api/Message
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public IActionResult PostMessage(Message newMessage)
        {
            throw new NotImplementedException();
        }

        // PUT: api/Message/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public IActionResult PutMessage(int id, Message message)
        {
            throw new NotImplementedException();
        }

        // DELETE: api/Message/5
        [HttpDelete("{id}")]
        public IActionResult DeleteMessage(int id)
        {
            throw new NotImplementedException();
        }
    }
}
