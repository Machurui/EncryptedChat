using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IMessageService
{
    Task<IReadOnlyList<MessageDTOPublic>?> GetAllByTeamAsync(Guid id, int page = 1, int pageSize = 50);
    Task<MessageDTOPublic?> GetByIdAsync(Guid id);
    Task<MessageDTOPublic?> CreateAsync(MessageDTO message, string senderId);
    Task<MessageDTOPublic?> UpdateAsync(Guid id, MessageDTO message, string actorId);
    Task<MessageDTOPublic?> DeleteAsync(Guid id, string actorId);
    Task<int> CountByTeamAsync(Guid teamId);
}
