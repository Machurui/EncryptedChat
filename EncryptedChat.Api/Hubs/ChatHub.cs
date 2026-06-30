using System.Security.Claims;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EncryptedChat.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ChatHub(IMessageService messageService, ITeamService teamService, IUserService userService, IFriendService friendService, IRealtimeService realtimeService, IPresenceService presenceService, IRateLimitService rateLimitService, ILogger<ChatHub> logger, IHubContext<ChatHub> hubContext) : Hub
{
    private const int MaxMessageLength = 4000;

    private readonly IMessageService _messageService = messageService;
    private readonly ITeamService _teamService = teamService;
    private readonly IUserService _userService = userService;
    private readonly IFriendService _friendService = friendService;
    private readonly IRealtimeService _realtimeService = realtimeService;
    private readonly IPresenceService _presenceService = presenceService;
    private readonly IRateLimitService _rateLimitService = rateLimitService;
    private readonly ILogger<ChatHub> _logger = logger;
    private readonly IHubContext<ChatHub> _hubContext = hubContext;

    private static string TeamGroup(Guid teamId) => $"team-{teamId}";

    private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public override async Task OnConnectedAsync()
    {
        string? userId = GetUserId();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // Track this connection
            _presenceService.AddConnection(userId, Context.ConnectionId);

            // Notify friends that this user is online
            await NotifyFriendsOfStatusChange(userId, "online");

            // Send this user the status of their online friends
            await SendOnlineFriendsStatus(userId);

            // Broadcast status to all team groups this user belongs to
            await BroadcastStatusToUserTeams(userId, isOnline: true);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            bool wasLast = _presenceService.RemoveConnection(userId, Context.ConnectionId);
            if (wasLast)
            {
                await _userService.UpdateLastSeenAsync(userId);
                await NotifyFriendsOfStatusChange(userId, "offline");
                await BroadcastStatusToUserTeams(userId, isOnline: false);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendOnlineFriendsStatus(string userId)
    {
        IReadOnlyList<FriendDTO> friends = await _friendService.GetFriendsAsync(userId);

        foreach (FriendDTO friend in friends)
        {
            if (_presenceService.IsOnline(friend.UserId))
            {
                UserProfileDTO? profile = await _userService.GetOwnProfileAsync(friend.UserId);
                if (profile == null) continue;

                string displayStatus = profile.Status == "invisible" ? "offline" : (string.IsNullOrEmpty(profile.Status) ? "online" : profile.Status);

                UserStatusUpdateDTO statusUpdate = new(
                    Id: profile.Id,
                    Name: profile.Name,
                    Handle: profile.Handle,
                    Level: profile.Level,
                    NameColor: profile.NameColor,
                    ProfileImageUrl: profile.ProfileImageUrl,
                    Status: displayStatus,
                    StatusMessage: profile.Status == "invisible" ? null : profile.StatusMessage,
                    LastSeenAt: null
                );

                await Clients.Caller.SendAsync("FriendProfileUpdated", statusUpdate);
            }
        }
    }

    private async Task NotifyFriendsOfStatusChange(string userId, string newStatus)
    {
        UserProfileDTO? profile = await _userService.GetOwnProfileAsync(userId);
        if (profile == null) return;

        string displayStatus = profile.Status == "invisible" ? "offline" : (newStatus == "offline" ? "offline" : profile.Status);
        DateTime? lastSeenAt = newStatus == "offline" ? DateTime.UtcNow : (DateTime?)null;

        UserStatusUpdateDTO statusUpdate = new(
            Id: profile.Id,
            Name: profile.Name,
            Handle: profile.Handle,
            Level: profile.Level,
            NameColor: profile.NameColor,
            ProfileImageUrl: profile.ProfileImageUrl,
            Status: displayStatus,
            StatusMessage: profile.Status == "invisible" ? null : profile.StatusMessage,
            LastSeenAt: lastSeenAt
        );

        IReadOnlyList<FriendDTO> friends = await _friendService.GetFriendsAsync(userId);
        List<string> friendIds = [.. friends.Select(f => f.UserId)];

        if (friendIds.Count > 0)
            await Clients.Users(friendIds).SendAsync("FriendProfileUpdated", statusUpdate);
    }

    private async Task BroadcastStatusToUserTeams(string userId, bool isOnline)
    {
        UserProfileDTO? profile = await _userService.GetOwnProfileAsync(userId);
        string effective = StatusHelper.EffectiveStatus(profile?.Status, isOnline);

        IReadOnlyList<UserTeamDTO> teams = await _userService.GetUserTeamsAsync(userId, userId);
        foreach (UserTeamDTO team in teams)
        {
            await Clients.Group($"team-{team.Id}").SendAsync(
                "TeamMemberStatusChanged",
                new { UserId = userId, Status = effective });
        }
    }

    public async Task JoinTeam(Guid teamId)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return;

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));
    }

    public async Task LeaveTeam(Guid teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(teamId));
    }

    public async Task<MessageDTOPublic?> SendMessageToTeam(Guid teamId, string encryptedText, string iv, string signature, int keyGeneration)
    {
        if (string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(iv) || string.IsNullOrEmpty(signature))
            return null;

        if (encryptedText.Length > MaxMessageLength * 2)
            return null;

        string? senderId = GetUserId();
        if (string.IsNullOrWhiteSpace(senderId))
            return null;

        RateLimitResult rateCheck = _rateLimitService.CheckAndRecord(senderId);
        if (!rateCheck.Allowed)
        {
            await Clients.Caller.SendAsync("RateLimited", rateCheck.RetryAfterMs);
            return null;
        }

        bool isMember = await _teamService.IsMemberAsync(senderId, teamId);
        if (!isMember)
            return null;

        MessageDTO dto = new()
        {
            Team = teamId,
            EncryptedText = encryptedText,
            Iv = iv,
            Signature = signature,
            KeyGeneration = keyGeneration
        };

        MessageDTOPublic? created = await _messageService.CreateAsync(dto, senderId);
        if (created is null)
            return null;

        await _hubContext.Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));

        await _realtimeService.BroadcastMessageAsync(teamId, created);

        TeamDTOPublic? teamDto = await _teamService.GetByIdAsync(teamId);
        if (teamDto?.IsDirect == true)
        {
            int messageCount = await _messageService.CountByTeamAsync(teamId);
            if (messageCount == 1)
            {
                IReadOnlyList<string> allMemberIds = await _teamService.GetMemberUserIdsAsync(teamId);
                string? friendId = allMemberIds.FirstOrDefault(id => id != senderId);
                if (!string.IsNullOrEmpty(friendId))
                {
                    await _hubContext.Clients.User(friendId).SendAsync("DirectMessageCreated", teamDto);
                    await _hubContext.Clients.User(friendId).SendAsync("ReceiveMessage", created);
                }
            }
        }

        IReadOnlyList<string> memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        if (memberIds.Count > 0)
            await _realtimeService.BroadcastTeamLastMessageAsync(teamId, memberIds, string.Empty, created.Date, created.Sender?.Name);

        return created;
    }

    public async Task StartTyping(Guid teamId)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return;

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return;

        UserProfileDTO? profile = await _userService.GetOwnProfileAsync(userId);
        if (profile == null || !profile.TypingIndicators)
            return;

        IReadOnlyList<string> memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        List<string> otherMembers = [.. memberIds.Where(id => id != userId)];

        if (otherMembers.Count > 0)
        {
            await Clients.Users(otherMembers).SendAsync("UserTyping", new
            {
                TeamId = teamId,
                UserId = userId,
                UserName = profile.Name
            });
        }
    }

    public async Task StopTyping(Guid teamId)
    {
        string? userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return;

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
            return;

        IReadOnlyList<string> memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        List<string> otherMembers = [.. memberIds.Where(id => id != userId)];

        if (otherMembers.Count > 0)
        {
            await Clients.Users(otherMembers).SendAsync("UserStoppedTyping", new
            {
                TeamId = teamId,
                UserId = userId
            });
        }
    }

    public record UserStatusUpdateDTO(
        string Id,
        string Name,
        string? Handle,
        int Level,
        string NameColor,
        string? ProfileImageUrl,
        string Status,
        string? StatusMessage,
        DateTime? LastSeenAt
    );
}
