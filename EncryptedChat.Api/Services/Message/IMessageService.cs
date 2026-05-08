using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IMessageService
{
    Task<IEnumerable<MessageDTOPublic>?> GetAllAsync();
    Task<IReadOnlyList<MessageDTOPublic>?> GetAllByTeamAsync(Guid id, int page = 1, int pageSize = 50);
    Task<MessageDTOPublic?> GetByIdAsync(int id);
    Task<MessageDTOPublic?> CreateAsync(MessageDTO message);
    Task<MessageDTOPublic?> UpdateAsync(int id, MessageDTO message);
    Task<MessageDTOPublic?> DeleteAsync(int id);
}
