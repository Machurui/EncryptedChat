using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IRealtimeService
{
    Task BroadcastMessageAsync(Guid teamId, MessageDTOPublic message);
    Task BroadcastAttachmentAddedAsync(Guid teamId, Guid messageId, AttachmentDTOPublic attachment);
    Task BroadcastTeamLastMessageAsync(Guid teamId, IReadOnlyList<string> memberIds, string preview, DateTime time, string? senderName);
    Task BroadcastLevelChangedAsync(string userId, int level, IReadOnlyList<Guid> teamIds);
}
