using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using EncryptedChat.Services;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DEV_DatabaseController : ControllerBase
    {

        private readonly EncryptedChatContext _context;

        public DEV_DatabaseController(EncryptedChatContext context)
        {
            _context = context;
        }

        [HttpDelete]
        public IActionResult DeleteDEV_Database(string table)
        {
            _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            _context.Database.ExecuteSqlRaw($"DELETE FROM {table}");
            _context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            _context.SaveChanges();

            return NoContent();
        }
    }
}
