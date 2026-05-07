using System.Security.Claims;
using EncryptedChat.Models;
using EncryptedChat.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EncryptedChat.Hubs
{
    // [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User")]
    public class ChatHub : Hub
    {
        private readonly IMessageService _messageService;

        public ChatHub(IMessageService messageService)
        {
            _messageService = messageService;
        }

        private static string TeamGroup(Guid teamId) => $"team-{teamId}";

        // Called by client to send a message to a team
        public async Task SendMessageToTeam(Guid teamId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            // User id from JWT
            var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(senderId))
                return;

            var dto = new MessageDTO
            {
                Text = text,
                Sender = senderId,
                Team = teamId
            };

            // Reuse YOUR service logic (validates user is in team, etc.)
            var created = await _messageService.CreateAsync(dto);
            if (created is null)
                return;

            // Broadcast the new message to everyone in this team
            await Clients.Group(TeamGroup(teamId))
                        .SendAsync("ReceiveMessage", created);
        }
    }
}
