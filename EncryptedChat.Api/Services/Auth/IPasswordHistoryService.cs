using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IPasswordHistoryService
{
    Task<bool> IsReusedAsync(User user, string candidatePlaintext);

    Task RecordAsync(string userId, string hashToRecord);
}
