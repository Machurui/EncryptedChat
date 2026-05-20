using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IRecoveryService
{
    Task<RecoveryPhraseDTO?> GenerateRecoveryPhraseAsync(string userId);
    Task<RecoveryPhraseDTO?> GetRecoveryPhraseAsync(string userId, string password);
    Task<bool> VerifyRecoveryPhraseAsync(string userId, IReadOnlyList<string> words);
    Task<DateTime?> GetLastViewedAsync(string userId);
    Task UpdateLastViewedAsync(string userId);
}
