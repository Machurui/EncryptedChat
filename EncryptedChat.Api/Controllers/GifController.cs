using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
    public sealed class GifController(IGifService gifService) : ControllerBase
    {
        private readonly IGifService _gifService = gifService;

        [HttpGet("search")]
        public async Task<ActionResult<List<GifResultDTO>>> Search(
            [FromQuery] string q,
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { Message = "Query is required." });
            if (limit is < 1 or > 50)
                return BadRequest(new { Message = "Limit must be between 1 and 50." });

            var results = await _gifService.SearchAsync(q.Trim(), limit, ct);
            return Ok(results);
        }
    }
}
