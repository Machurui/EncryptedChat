using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IPinnedMessageService
{
    Task<List<PinnedMessageDTO>> GetPinnedMessagesAsync(Guid teamId, string userId);
    Task<PinnedMessageDTO?> PinMessageAsync(Guid teamId, Guid messageId, string userId);
    Task<bool> UnpinMessageAsync(Guid teamId, Guid messageId, string userId);
    Task<int> GetPinnedCountAsync(Guid teamId);
}
