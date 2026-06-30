using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Models;
using EncryptedChat.Services;
using EncryptedChat.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EncryptedChat.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendController(
    IFriendService friendService,
    IUserService userService,
    IHubContext<ChatHub> hubContext,
    ILogger<FriendController> logger) : ControllerBase
{
    private readonly IFriendService _friendService = friendService;
    private readonly IUserService _userService = userService;
    private readonly IHubContext<ChatHub> _hubContext = hubContext;
    private readonly ILogger<FriendController> _logger = logger;

    private string? GetCurrentUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetFriends()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        IReadOnlyList<FriendDTO> friends = await _friendService.GetFriendsAsync(userId);
        return Ok(friends);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        IReadOnlyList<FriendRequestDTO> requests = await _friendService.GetPendingRequestsAsync(userId);
        return Ok(requests);
    }

    [HttpPost("{addresseeId}")]
    public async Task<IActionResult> SendRequest(string addresseeId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (userId == addresseeId)
            return BadRequest(new { Message = "You cannot send a friend request to yourself." });

        FriendRequestDTO? request = await _friendService.SendRequestAsync(userId, addresseeId);
        if (request == null)
            return BadRequest(new { Message = "Could not send friend request. User may not exist or request already exists." });

        _logger.LogInformation("[FriendController] Broadcasting FriendRequestReceived to UserId={AddresseeId}, RequestId={RequestId}", addresseeId, request.RequestId);
        await _hubContext.Clients.User(addresseeId).SendAsync("FriendRequestReceived", request);

        return Ok(new { Message = "Friend request sent." });
    }

    [HttpPost("requests/{requestId}/accept")]
    public async Task<IActionResult> AcceptRequest(Guid requestId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        (bool, string?, FriendDTO?) acceptedRequest = await _friendService.AcceptRequestAsync(userId, requestId);
        if (!acceptedRequest.Item1)
            return NotFound(new { Message = "Request not found or already processed." });

        if (!string.IsNullOrEmpty(acceptedRequest.Item2) && acceptedRequest.Item3 != null)
        {
            _logger.LogInformation("[FriendController] Broadcasting FriendRequestAccepted to UserId={RequesterId}, RequestId={RequestId}", acceptedRequest.Item2, requestId);
            await _hubContext.Clients.User(acceptedRequest.Item2).SendAsync("FriendRequestAccepted", requestId, acceptedRequest.Item3);
        }

        return Ok(new { Message = "Friend request accepted." });
    }

    [HttpPost("requests/{requestId}/reject")]
    public async Task<IActionResult> RejectRequest(Guid requestId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        (bool, string?) rejectedRequest = await _friendService.RejectRequestAsync(userId, requestId);
        if (!rejectedRequest.Item1)
            return NotFound(new { Message = "Request not found or already processed." });

        if (!string.IsNullOrEmpty(rejectedRequest.Item2))
        {
            _logger.LogInformation("[FriendController] Broadcasting FriendRequestCancelled to UserId={OtherUserId}, RequestId={RequestId}", rejectedRequest.Item2, requestId);
            await _hubContext.Clients.User(rejectedRequest.Item2).SendAsync("FriendRequestCancelled", requestId);
        }

        return Ok(new { Message = "Friend request rejected." });
    }

    [HttpDelete("{friendId}")]
    public async Task<IActionResult> RemoveFriend(string friendId)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        (bool, string?, Guid?) removeRequest = await _friendService.RemoveFriendAsync(userId, friendId);
        if (!removeRequest.Item1)
            return NotFound(new { Message = "Friendship not found." });

        if (!string.IsNullOrEmpty(removeRequest.Item2))
        {
            await _hubContext.Clients.User(removeRequest.Item2).SendAsync("FriendRemoved", userId);

            if (removeRequest.Item3.HasValue)
            {
                string[] bothUsers = [userId, removeRequest.Item2];
                await _hubContext.Clients.Users(bothUsers)
                    .SendAsync("TeamDeleted", new { TeamId = removeRequest.Item3.Value });
            }
        }

        return NoContent();
    }

    // Search within the caller's own friends (partial, in-memory). Distinct from
    // search-users, which finds strangers to add via an exact blind-index lookup.
    [HttpGet("search")]
    public async Task<IActionResult> SearchFriends([FromQuery] string? q, [FromQuery] int limit = 20)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        IReadOnlyList<FriendDTO> friends = await _friendService.SearchFriendsAsync(userId, q, limit);
        return Ok(friends);
    }

    [HttpGet("search-users")]
    public async Task<IActionResult> SearchUsersToAdd([FromQuery] string q, [FromQuery] int limit = 10)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserDTOPublic>());

        IReadOnlyList<UserDTOPublic> allUsers = await _userService.SearchUsersAsync(q, userId, limit);

        List<UserDTOPublic> result = [];
        foreach (UserDTOPublic user in allUsers)
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
