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

    private readonly IMessageService _messageService;
    private readonly ITeamService _teamService;
    private readonly IUserService _userService;
    private readonly IFriendService _friendService;
    private readonly IRealtimeService _realtimeService;
    private readonly IPresenceService _presenceService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<ChatHub> _logger;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatHub(IMessageService messageService, ITeamService teamService, IUserService userService, IFriendService friendService, IRealtimeService realtimeService, IPresenceService presenceService, IRateLimitService rateLimitService, ILogger<ChatHub> logger, IHubContext<ChatHub> hubContext)
    {
        _messageService = messageService;
        _teamService = teamService;
        _userService = userService;
        _friendService = friendService;
        _realtimeService = realtimeService;
        _presenceService = presenceService;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _hubContext = hubContext;
    }

    private static string TeamGroup(Guid teamId) => $"team-{teamId}";

    private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public override async Task OnConnectedAsync()
    {
        string? userId = GetUserId();
        _logger.LogInformation("[ChatHub] OnConnectedAsync: UserId={UserId}, ConnectionId={ConnectionId}", userId, Context.ConnectionId);

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
        var friends = await _friendService.GetFriendsAsync(userId);

        foreach (var friend in friends)
        {
            if (_presenceService.IsOnline(friend.UserId))
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

    private async Task BroadcastStatusToUserTeams(string userId, bool isOnline)
    {
        var profile = await _userService.GetOwnProfileAsync(userId);
        var effective = StatusHelper.EffectiveStatus(profile?.Status, isOnline);

        var teams = await _userService.GetUserTeamsAsync(userId, userId);
        foreach (var team in teams)
        {
            await Clients.Group($"team-{team.Id}").SendAsync(
                "TeamMemberStatusChanged",
                new { UserId = userId, Status = effective });
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

    public async Task<MessageDTOPublic?> SendMessageToTeam(Guid teamId, string encryptedText, string iv, string signature, int keyGeneration)
    {
        if (string.IsNullOrEmpty(encryptedText) || string.IsNullOrEmpty(iv) || string.IsNullOrEmpty(signature))
            return null;

        if (encryptedText.Length > MaxMessageLength * 2)
            return null;

        string? senderId = GetUserId();
        if (string.IsNullOrWhiteSpace(senderId))
            return null;

        var rateCheck = _rateLimitService.CheckAndRecord(senderId);
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

        // Ensure the sender's connection is in the team group BEFORE broadcasting.
        // Required for fresh DMs where the client-side JoinTeam may not have
        // completed (race with hubConnection state) or for any robustness gap.
        // Idempotent — safe to call every time.
        await _hubContext.Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));

        await _realtimeService.BroadcastMessageAsync(teamId, created);

        // First message in a DM? Notify the friend directly so they receive both
        // (a) the new DM in their sidebar and (b) the initial message in real time.
        // (Required because TeamController no longer broadcasts DirectMessageCreated
        // on bare DM creation — the friend is only notified once there's content.)
        var teamDto = await _teamService.GetByIdAsync(teamId);
        if (teamDto?.IsDirect == true)
        {
            int messageCount = await _messageService.CountByTeamAsync(teamId);
            if (messageCount == 1)
            {
                var allMemberIds = await _teamService.GetMemberUserIdsAsync(teamId);
                var friendId = allMemberIds.FirstOrDefault(id => id != senderId);
                if (!string.IsNullOrEmpty(friendId))
                {
                    await _hubContext.Clients.User(friendId).SendAsync("DirectMessageCreated", teamDto);
                    await _hubContext.Clients.User(friendId).SendAsync("ReceiveMessage", created);
                }
            }
        }

        var memberIds = await _teamService.GetMemberUserIdsAsync(teamId);
        if (memberIds.Count > 0)
        {
            // Server can no longer read the plaintext (true E2E). Clients
            // render their own preview from the message they just decrypted.
            await _realtimeService.BroadcastTeamLastMessageAsync(teamId, memberIds, string.Empty, created.Date, created.Sender?.Name);
        }

        // Return the saved message so the sender can render it locally — robust
        // even if the group broadcast didn't reach them (e.g., fresh DM race).
        // The client uses message.Id to dedup against the eventual ReceiveMessage.
        return created;
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
