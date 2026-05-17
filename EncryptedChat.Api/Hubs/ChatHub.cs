using System.Collections.Concurrent;
using System.Security.Claims;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EncryptedChat.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ChatHub : Hub
{
    private const int MaxMessageLength = 4000;

    private static readonly ConcurrentDictionary<string, HashSet<string>> _connectedUsers = new();

    private readonly IMessageService _messageService;
    private readonly ITeamService _teamService;
    private readonly IUserService _userService;
    private readonly IFriendService _friendService;
    private readonly IRealtimeService _realtimeService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMessageService messageService, ITeamService teamService, IUserService userService, IFriendService friendService, IRealtimeService realtimeService, ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _teamService = teamService;
        _userService = userService;
        _friendService = friendService;
        _realtimeService = realtimeService;
        _logger = logger;
    }

    private static string TeamGroup(Guid teamId) => $"team-{teamId}";

    public static bool IsUserOnline(string userId) => _connectedUsers.ContainsKey(userId) && _connectedUsers[userId].Count > 0;

    private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public override async Task OnConnectedAsync()
    {
        string? userId = GetUserId();
        _logger.LogInformation("[ChatHub] OnConnectedAsync: UserId={UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            // Track this connection
            _connectedUsers.AddOrUpdate(
                userId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, connections) => { connections.Add(Context.ConnectionId); return connections; }
            );

            // Notify friends that this user is online
            await NotifyFriendsOfStatusChange(userId, "online");

            // Send this user the status of their online friends
            await SendOnlineFriendsStatus(userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? userId = GetUserId();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            if (_connectedUsers.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    _connectedUsers.TryRemove(userId, out _);
                    await _userService.UpdateLastSeenAsync(userId);
                    await NotifyFriendsOfStatusChange(userId, "offline");
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendOnlineFriendsStatus(string userId)
    {
        var friends = await _friendService.GetFriendsAsync(userId);

        foreach (var friend in friends)
        {
            if (IsUserOnline(friend.UserId))
            {
                var profile = await _userService.GetOwnProfileAsync(friend.UserId);
                if (profile == null) continue;

                var displayStatus = profile.Status == "invisible" ? "offline" : (string.IsNullOrEmpty(profile.Status) ? "online" : profile.Status);

                var statusUpdate = new
                {
                    Id = profile.Id,
                    profile.Name,
                    profile.Handle,
                    profile.Level,
                    profile.NameColor,
                    profile.ProfileImageUrl,
                    Status = displayStatus,
                    StatusMessage = profile.Status == "invisible" ? (string?)null : profile.StatusMessage
                };

                await Clients.Caller.SendAsync("FriendProfileUpdated", statusUpdate);
            }
        }
    }

    private async Task NotifyFriendsOfStatusChange(string userId, string newStatus)
    {
        var profile = await _userService.GetOwnProfileAsync(userId);
        if (profile == null) return;

        var displayStatus = profile.Status == "invisible" ? "offline" : (newStatus == "offline" ? "offline" : profile.Status);
        var lastSeenAt = newStatus == "offline" ? DateTime.UtcNow : (DateTime?)null;

        var statusUpdate = new
        {
            profile.Id,
            profile.Name,
            profile.Handle,
            profile.Level,
            profile.NameColor,
            profile.ProfileImageUrl,
            Status = displayStatus,
            StatusMessage = profile.Status == "invisible" ? (string?)null : profile.StatusMessage,
            LastSeenAt = lastSeenAt
        };

        var friends = await _friendService.GetFriendsAsync(userId);
        var friendIds = friends.Select(f => f.UserId).ToList();

        if (friendIds.Count > 0)
        {
            await Clients.Users(friendIds).SendAsync("FriendProfileUpdated", statusUpdate);
        }
    }

    public async Task JoinTeam(Guid teamId)
    {
        string? userId = GetUserId();
        _logger.LogInformation("[JoinTeam] UserId={UserId}, TeamId={TeamId}, ConnectionId={ConnectionId}", userId, teamId, Context.ConnectionId);

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[JoinTeam] No userId, cannot join");
            return;
        }

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
        {
            _logger.LogWarning("[JoinTeam] User {UserId} is not a member of team {TeamId}", userId, teamId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));
        _logger.LogInformation("[JoinTeam] Added connection {ConnectionId} to group {Group}", Context.ConnectionId, TeamGroup(teamId));
    }

    public async Task LeaveTeam(Guid teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(teamId));
    }

    public async Task SendMessageToTeam(Guid teamId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > MaxMessageLength)
            return;

        string? senderId = GetUserId();
        if (string.IsNullOrWhiteSpace(senderId))
            return;

        bool isMember = await _teamService.IsMemberAsync(senderId, teamId);
        if (!isMember)
            return;

        MessageDTO dto = new()
        {
            Text = text,
            Team = teamId
        };

        MessageDTOPublic? created = await _messageService.CreateAsync(dto, senderId);
        if (created is null)
            return;

        await _realtimeService.BroadcastMessageAsync(teamId, created);

        var memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        if (memberIds.Count > 0)
        {
            var preview = text.Length > 50 ? text[..50] + "..." : text;
            preview = preview.Replace("\n", " ").Trim();
            await _realtimeService.BroadcastTeamLastMessageAsync(teamId, memberIds, preview, created.Date, created.Sender?.Name);
        }
    }

    public async Task StartTyping(Guid teamId)
    {
        string? userId = GetUserId();
        _logger.LogInformation("[StartTyping] UserId: {UserId}, TeamId: {TeamId}", userId, teamId);

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("[StartTyping] No userId found in claims");
            return;
        }

        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember)
        {
            _logger.LogWarning("[StartTyping] User {UserId} is not a member of team {TeamId}", userId, teamId);
            return;
        }

        var profile = await _userService.GetOwnProfileAsync(userId);
        if (profile == null || !profile.TypingIndicators)
        {
            _logger.LogInformation("[StartTyping] Profile null or TypingIndicators disabled. Profile exists: {Exists}, TypingIndicators: {Setting}",
                profile != null, profile?.TypingIndicators);
            return;
        }

        var memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        var otherMembers = memberIds.Where(id => id != userId).ToList();
        _logger.LogInformation("[StartTyping] Sending to {Count} other members: {Members}", otherMembers.Count, string.Join(", ", otherMembers));

        if (otherMembers.Count > 0)
        {
            await Clients.Users(otherMembers).SendAsync("UserTyping", new
            {
                TeamId = teamId,
                UserId = userId,
                UserName = profile.Name
            });
            _logger.LogInformation("[StartTyping] UserTyping event sent for {UserName}", profile.Name);
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

        var memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        var otherMembers = memberIds.Where(id => id != userId).ToList();

        if (otherMembers.Count > 0)
        {
            await Clients.Users(otherMembers).SendAsync("UserStoppedTyping", new
            {
                TeamId = teamId,
                UserId = userId
            });
        }
    }
}
