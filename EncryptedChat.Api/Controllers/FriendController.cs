using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendController(IFriendService friendService, IUserService userService) : ControllerBase
{
    private readonly IFriendService _friendService = friendService;
    private readonly IUserService _userService = userService;

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var friends = await _friendService.GetFriendsAsync(userId);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var requests = await _friendService.GetPendingRequestsAsync(userId);
        return Ok(requests);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchFriends([FromQuery] string q, [FromQuery] int limit = 10)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserDTOPublic>());

        var friends = await _friendService.SearchFriendsAsync(userId, q, limit);
        return Ok(friends);
    }

    [HttpPost("{addresseeId}")]
    public async Task<IActionResult> SendRequest(string addresseeId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (userId == addresseeId)
            return BadRequest(new { Message = "You cannot send a friend request to yourself." });

        bool success = await _friendService.SendRequestAsync(userId, addresseeId);
        if (!success)
            return BadRequest(new { Message = "Could not send friend request. User may not exist or request already exists." });

        return Ok(new { Message = "Friend request sent." });
    }

    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptRequest(Guid requestId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool success = await _friendService.AcceptRequestAsync(userId, requestId);
        if (!success)
            return NotFound(new { Message = "Request not found or already processed." });

        return Ok(new { Message = "Friend request accepted." });
    }

    [HttpPost("requests/{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(Guid requestId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool success = await _friendService.RejectRequestAsync(userId, requestId);
        if (!success)
            return NotFound(new { Message = "Request not found or already processed." });

        return Ok(new { Message = "Friend request rejected." });
    }

    [HttpDelete("{friendId}")]
    public async Task<IActionResult> RemoveFriend(string friendId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        bool success = await _friendService.RemoveFriendAsync(userId, friendId);
        if (!success)
            return NotFound(new { Message = "Friendship not found." });

        return NoContent();
    }

    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsersToAdd([FromQuery] string q, [FromQuery] int limit = 10)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserDTOPublic>());

        var allUsers = await _userService.SearchUsersAsync(q, userId, limit);

        var result = new List<UserDTOPublic>();
        foreach (var user in allUsers)
        {
            bool areFriends = await _friendService.AreFriendsAsync(userId, user.Id);
            if (!areFriends)
            {
                result.Add(user);
            }
        }

        return Ok(result);
    }
}
