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
            [FromQuery] int offset = 0,
            [FromQuery] string type = "gifs",
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q))
                return BadRequest(new { Message = "Query is required." });
            if (limit is < 1 or > 50)
                return BadRequest(new { Message = "Limit must be between 1 and 50." });
            if (offset < 0)
                return BadRequest(new { Message = "Offset must be >= 0." });
            if (!TryStickers(type, out bool stickers))
                return BadRequest(new { Message = "Type must be 'gifs' or 'stickers'." });

            List<GifResultDTO> results = await _gifService.SearchAsync(q.Trim(), limit, offset, stickers, ct);
            return Ok(results);
        }

        [HttpGet("trending")]
        public async Task<ActionResult<List<GifResultDTO>>> Trending(
            [FromQuery] int limit = 20,
            [FromQuery] int offset = 0,
            [FromQuery] string type = "gifs",
            CancellationToken ct = default)
        {
            if (limit is < 1 or > 50)
                return BadRequest(new { Message = "Limit must be between 1 and 50." });
            if (offset < 0)
                return BadRequest(new { Message = "Offset must be >= 0." });
            if (!TryStickers(type, out bool stickers))
                return BadRequest(new { Message = "Type must be 'gifs' or 'stickers'." });

            List<GifResultDTO> results = await _gifService.TrendingAsync(limit, offset, stickers, ct);
            return Ok(results);
        }

        [HttpGet("categories")]
        public async Task<ActionResult<List<GifCategoryDTO>>> Categories(CancellationToken ct = default)
        {
            List<GifCategoryDTO> results = await _gifService.CategoriesAsync(ct);
            return Ok(results);
        }

        [HttpGet("random")]
        public async Task<ActionResult<GifResultDTO>> Random(
            [FromQuery] string? tag = null,
            [FromQuery] string type = "gifs",
            CancellationToken ct = default)
        {
            if (tag is { Length: > 100 })
                return BadRequest(new { Message = "Tag too long." });
            if (!TryStickers(type, out bool stickers))
                return BadRequest(new { Message = "Type must be 'gifs' or 'stickers'." });

            GifResultDTO? result = await _gifService.RandomAsync(tag, stickers, ct);
            return result is null ? NotFound(new { Message = "No GIF found." }) : Ok(result);
        }

        private static bool TryStickers(string type, out bool stickers)
        {
            stickers = type == "stickers";
            return type is "gifs" or "stickers";
        }
    }
}
