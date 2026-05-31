using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IUserKeysService
{
    // Returns null if the user has never set up keys (post-migration legacy state).
    Task<EncryptionKeysDTO?> GetMyKeysAsync(string userId);

    // Atomically writes all four key fields. Treats the user's whole bundle as
    // one unit (no partial updates).
    Task<bool> SetMyKeysAsync(string userId, SetEncryptionKeysDTO dto);

    // Returns null when either pubkey is missing (user hasn't bootstrapped yet).
    Task<PublicKeysDTO?> GetPublicKeysAsync(string userId);
}
