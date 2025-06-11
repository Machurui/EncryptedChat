using EncryptedChat.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Services;
using System.Threading.Tasks;
using System.Security.Claims;

namespace EncryptedChat.SignalR
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly EncryptedChatContext _context;
        private readonly IMessageService _messageService;

        public ChatHub(EncryptedChatContext context, IMessageService messageService)
        {
            _context = context;
            _messageService = messageService;
        }

        public override async Task OnConnectedAsync()
        {
            var userName = Context.User?.Identity?.Name;
            // Uncomment for debugging purposes
            // Console.WriteLine($"[SignalR] Connecting user: {userName}");

            if (string.IsNullOrEmpty(userName)) return;

            var user = await _context.Users
                .Include(u => u.TeamsAsAdmin)
                .Include(u => u.TeamsAsMember)
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user != null)
            {
                var allTeams = user.TeamsAsAdmin.Concat(user.TeamsAsMember).Distinct();
                foreach (var team in allTeams)
                {
                    var groupName = $"team-{team.Id}";
                    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userName = Context.User?.Identity?.Name;
            // Uncomment for debugging purposes
            // Console.WriteLine($"[SignalR] Disconnecting user: {userName}");

            if (string.IsNullOrEmpty(userName)) return;

            var user = await _context.Users
                .Include(u => u.TeamsAsAdmin)
                .Include(u => u.TeamsAsMember)
                .FirstOrDefaultAsync(u => u.UserName == userName);

            if (user != null)
            {
                var allTeams = user.TeamsAsAdmin.Concat(user.TeamsAsMember).Distinct();
                foreach (var team in allTeams)
                {
                    var groupName = $"team-{team.Id}";
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // public async Task Leave(string roomName)
        // {
        //     await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        // }

        public async Task SendMessage(int teamId, string encryptedText)
        {
            var userName = Context.User?.Identity?.Name;
            var user = await _context.Users
                .Include(u => u.TeamsAsAdmin)
                .Include(u => u.TeamsAsMember)
                .FirstOrDefaultAsync(u => u.UserName == userName)
                ?? throw new HubException("User not found.");

            var isInTeam = user.TeamsAsAdmin.Any(t => t.Id == teamId)
                        || user.TeamsAsMember.Any(t => t.Id == teamId);

            if (!isInTeam)
                throw new HubException("Not authorized to send messages to this team.");

            if (string.IsNullOrWhiteSpace(encryptedText))
                throw new HubException("Message text cannot be empty.");

            var messageDto = new MessageDTO
            {
                Text = encryptedText,
                Team = teamId,
                Sender = user.Id
            };

            var savedMessage = await _messageService.CreateAsync(messageDto);
            if (savedMessage == null)
                throw new HubException("Invalid message or permissions.");

            await Clients.Group($"team-{teamId}")
                .SendAsync("ReceiveMessage", teamId, encryptedText, savedMessage?.Sender?.Email, savedMessage?.Date);
        }
    }

}
