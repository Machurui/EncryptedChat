using EncryptedChat.Hubs;
using EncryptedChat.Models;
using Microsoft.AspNetCore.SignalR;

namespace EncryptedChat.Services;

public class RealtimeService(IHubContext<ChatHub> hubContext, ILogger<RealtimeService> logger) : IRealtimeService
{
    private static string TeamGroup(Guid teamId) => $"team-{teamId}";

    public async Task BroadcastMessageAsync(Guid teamId, MessageDTOPublic message)
    {
        try
        {
            await hubContext.Clients.Group(TeamGroup(teamId)).SendAsync("ReceiveMessage", message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast message {MessageId} to team {TeamId}", message.Id, teamId);
        }
    }

    public async Task BroadcastAttachmentAddedAsync(Guid teamId, Guid messageId, AttachmentDTOPublic attachment)
    {
        try
        {
            await hubContext.Clients.Group(TeamGroup(teamId)).SendAsync("AttachmentAdded", new { MessageId = messageId, Attachment = attachment });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast attachment for message {MessageId}", messageId);
        }
    }

    public async Task BroadcastTeamLastMessageAsync(Guid teamId, IReadOnlyList<string> memberIds, string preview, DateTime time, string? senderName)
    {
        if (memberIds.Count == 0) return;

        try
        {
            LastMessageDTO update = new
            (
                Id: teamId,
                LastMessagePreview: preview,
                LastMessageTime: time,
                LastMessageSenderName: senderName
            );
            await hubContext.Clients.Users(memberIds).SendAsync("TeamLastMessageUpdated", update);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast last message update for team {TeamId}", teamId);
        }
    }

    public async Task BroadcastLevelChangedAsync(string userId, int level, IReadOnlyList<Guid> teamIds)
    {
        if (teamIds.Count == 0) return;

        try
        {
            LevelChangedDTO payload = new(
                UserId: userId,
                Level: level
            );
            // One emit per team group: reaches the user themself (they're in their own
            // team groups => self-update) AND their connected teammates.
            foreach (Guid teamId in teamIds)
            {
                await hubContext.Clients.Group(TeamGroup(teamId)).SendAsync("LevelChanged", payload);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast level change for user {UserId}", userId);
        }
    }

    public record LastMessageDTO
    (
        Guid Id,
        string LastMessagePreview,
        DateTime LastMessageTime,
        string? LastMessageSenderName
    );

    public record LevelChangedDTO
    (
        string UserId,
        int Level
    );
}
