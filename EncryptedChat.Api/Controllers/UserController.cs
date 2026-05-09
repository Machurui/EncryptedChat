using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController(IUserService userService) : ControllerBase
{
    private readonly IUserService _service = userService;

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        UserProfileDTO? user = await _service.GetOwnProfileAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UserUpdateDTO dto)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        UserUpdateResult result = await _service.UpdateAsync(userId, userId, dto);

        return result.Status switch
        {
            UserOperationStatus.Success => Ok(result.User),
            UserOperationStatus.NotFound => NotFound(),
            UserOperationStatus.Conflict => Conflict(new { Message = "Name or email already in use" }),
            UserOperationStatus.Forbidden => Forbid(),
            UserOperationStatus.ValidationFailed => BadRequest(new { Message = "Invalid request" }),
            _ => BadRequest()
        };
    }

    [HttpGet("me/teams")]
    public async Task<IActionResult> GetMyTeams([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        IReadOnlyList<UserTeamDTO> teams = await _service.GetUserTeamsAsync(userId, userId, page, pageSize);
        return Ok(teams);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 10)
    {
        string? requesterId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(requesterId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserDTOPublic>());

        IReadOnlyList<UserDTOPublic> users = await _service.SearchUsersAsync(q, requesterId, limit);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        string? requesterId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(requesterId))
            return Unauthorized();

        UserDTOPublic? user = await _service.GetUserAsync(id, requesterId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        string? currentUserId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        UserDeleteResult result = await _service.DeleteAsync(id, currentUserId);

        return result.Status switch
        {
            UserOperationStatus.Success => NoContent(),
            UserOperationStatus.NotFound => NotFound(),
            UserOperationStatus.Forbidden => Forbid(),
            UserOperationStatus.ValidationFailed => BadRequest(new { Message = "Cannot delete this user" }),
            _ => BadRequest()
        };
    }
}
