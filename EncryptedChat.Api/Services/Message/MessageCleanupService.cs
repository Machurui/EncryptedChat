using EncryptedChat.Data;
using EncryptedChat.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class MessageCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public MessageCleanupService(IServiceProvider serviceProvider, ILogger<MessageCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired messages");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EncryptedChatContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();

        var teamsWithLifetime = await context.Teams
            .AsNoTracking()
            .Where(t => t.MessageLifetime != "off")
            .Select(t => new { t.Id, t.MessageLifetime })
            .ToListAsync(cancellationToken);

        foreach (var team in teamsWithLifetime)
        {
            var cutoffDate = GetCutoffDate(team.MessageLifetime);
            if (cutoffDate == null) continue;

            var expiredMessages = await context.Messages
                .Where(m => m.Team != null && m.Team.Id == team.Id && m.Date < cutoffDate.Value)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            if (expiredMessages.Count == 0) continue;

            await context.Messages
                .Where(m => expiredMessages.Contains(m.Id))
                .ExecuteDeleteAsync(cancellationToken);

            var memberIds = await context.Members
                .AsNoTracking()
                .Where(m => m.TeamId == team.Id)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);

            if (memberIds.Count > 0)
            {
                await hubContext.Clients.Users(memberIds).SendAsync(
                    "MessagesExpired",
                    new { TeamId = team.Id, MessageIds = expiredMessages },
                    cancellationToken);
            }

            _logger.LogInformation("Deleted {Count} expired messages from team {TeamId}", expiredMessages.Count, team.Id);
        }
    }

    private static DateTime? GetCutoffDate(string messageLifetime)
    {
        var now = DateTime.UtcNow;
        return messageLifetime switch
        {
            "24h" => now.AddHours(-24),
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            _ => null
        };
    }
}
