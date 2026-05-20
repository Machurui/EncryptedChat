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
public class UserController(
    IUserService userService,
    IFriendService friendService,
    IHubContext<ChatHub> hubContext,
    IWebHostEnvironment env) : ControllerBase
{
    private readonly IUserService _service = userService;
    private readonly IFriendService _friendService = friendService;
    private readonly IHubContext<ChatHub> _hubContext = hubContext;
    private readonly IWebHostEnvironment _env = env;

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

        if (result.Status == UserOperationStatus.Success && result.User != null)
        {
            // Notify friends of profile update
            await NotifyFriendsOfProfileUpdate(userId, result.User);
            return Ok(result.User);
        }

        return result.Status switch
        {
            UserOperationStatus.NotFound => NotFound(),
            UserOperationStatus.Conflict => Conflict(new { Message = "Name, handle or email already in use" }),
            UserOperationStatus.Forbidden => Forbid(),
            UserOperationStatus.ValidationFailed => BadRequest(new { Message = "Invalid request" }),
            _ => BadRequest()
        };
    }

    [HttpPost("me/avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "No file provided" });

        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { Message = "Invalid file type. Allowed: jpg, jpeg, png, gif, webp" });

        string[] allowedMimeTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"];
        if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            return BadRequest(new { Message = "Invalid content type" });

        string uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "avatars");
        Directory.CreateDirectory(uploadsFolder);

        string fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
        string filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        string imageUrl = $"/uploads/avatars/{fileName}";

        UserUpdateResult result = await _service.UpdateAsync(userId, userId, new UserUpdateDTO { ProfileImageUrl = imageUrl });

        if (result.Status == UserOperationStatus.Success && result.User != null)
        {
            await NotifyFriendsOfProfileUpdate(userId, result.User);
            return Ok(new { Url = imageUrl, Profile = result.User });
        }

        return BadRequest(new { Message = "Failed to update profile" });
    }

    [HttpDelete("me/avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        string? userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        UserUpdateResult result = await _service.UpdateAsync(userId, userId, new UserUpdateDTO { ProfileImageUrl = "" });

        if (result.Status == UserOperationStatus.Success && result.User != null)
        {
            await NotifyFriendsOfProfileUpdate(userId, result.User);
            return Ok(result.User);
        }

        return BadRequest(new { Message = "Failed to remove avatar" });
    }

    private async Task NotifyFriendsOfProfileUpdate(string userId, UserProfileDTO profile)
    {
        var publicProfile = new
        {
            profile.Id,
            profile.Name,
            profile.Handle,
            profile.Level,
            profile.NameColor,
            profile.ProfileImageUrl,
            Status = profile.Status == "invisible" ? "offline" : profile.Status,
            StatusMessage = profile.Status == "invisible" ? null : profile.StatusMessage
        };

        // Notify friends
        var friends = await _friendService.GetFriendsAsync(userId);
        var friendIds = friends.Select(f => f.UserId).ToList();

        if (friendIds.Count > 0)
        {
            await _hubContext.Clients.Users(friendIds).SendAsync("FriendProfileUpdated", publicProfile);
        }

        // Notify users with pending friend requests
        var pendingUserIds = await _friendService.GetPendingRequestUserIdsAsync(userId);
        if (pendingUserIds.Count > 0)
        {
            await _hubContext.Clients.Users(pendingUserIds).SendAsync("FriendRequestProfileUpdated", publicProfile);
        }

        // Notify team members via team groups
        var teams = await _service.GetUserTeamsAsync(userId, userId, 1, 100);
        foreach (var team in teams)
        {
            await _hubContext.Clients.Group($"team-{team.Id}").SendAsync("FriendProfileUpdated", publicProfile);
        }
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
