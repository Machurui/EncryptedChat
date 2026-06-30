using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EncryptedChat.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
    public sealed class GifVaultController(IGifVaultService vaultService) : ControllerBase
    {
        private const int MaxBlobLength = 256 * 1024;
        private const int MaxWrappedKeyLength = 512;
        private const int MaxIvLength = 24;

        private readonly IGifVaultService _vaultService = vaultService;

        [HttpGet]
        public async Task<ActionResult<GifVaultReadDTO>> Get(CancellationToken ct)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            GifVaultReadDTO? vault = await _vaultService.GetAsync(userId, ct);
            return vault is null ? NoContent() : Ok(vault);
        }

        [HttpPut]
        public async Task<ActionResult> Put([FromBody] GifVaultWriteDTO dto, CancellationToken ct)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrEmpty(dto.WrappedKey) || dto.WrappedKey.Length > MaxWrappedKeyLength)
                return BadRequest(new { Message = "Invalid wrapped key." });
            if (string.IsNullOrEmpty(dto.Iv) || dto.Iv.Length > MaxIvLength)
                return BadRequest(new { Message = "Invalid IV." });
            if (string.IsNullOrEmpty(dto.Blob) || dto.Blob.Length > MaxBlobLength)
                return BadRequest(new { Message = "Invalid blob." });
            if (dto.ExpectedRevision < 0)
                return BadRequest(new { Message = "Invalid revision." });

            GifVaultUpsertResult result = await _vaultService.UpsertAsync(userId, dto, ct);
            return result.Kind == GifVaultUpsertKind.Conflict
                ? Conflict(new { result.Revision })
                : Ok(new { result.Revision });
        }
    }
}
