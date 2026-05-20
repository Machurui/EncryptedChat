namespace EncryptedChat.Services;

public interface IPresenceService
{
    bool IsOnline(string userId);
    void AddConnection(string userId, string connectionId);
    bool RemoveConnection(string userId, string connectionId);
}
