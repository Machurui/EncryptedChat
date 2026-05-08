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

    public ChatHub(IMessageService messageService, ITeamService teamService)
    {
        _messageService = messageService;
        _teamService = teamService;
    }

    private static string TeamGroup(Guid teamId) => $"team-{teamId}";

    private string? GetUserId() => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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

        await Clients.Group(TeamGroup(teamId)).SendAsync("ReceiveMessage", created);
    }
}
