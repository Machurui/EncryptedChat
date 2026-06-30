using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EncryptedChat.Services;

public class RecoveryService(EncryptedChatContext context, UserManager<User> userManager) : IRecoveryService
{
    private readonly EncryptedChatContext _context = context;
    private readonly UserManager<User> _userManager = userManager;

    private const int Pbkdf2Iterations = 600_000;
    private const int SaltSizeBytes = 16;
    private const int DerivedKeySizeBytes = 32;
    private const int WordCount = 12;

    public async Task<RecoveryPhraseDTO?> GenerateRecoveryPhraseAsync(string userId)
    {
        User? user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        IReadOnlyList<string> words = GenerateRandomWords(WordCount);
        (string, string) hashPhrase = HashPhrase(words);

        user.RecoveryPhraseHash = hashPhrase.Item1;
        user.RecoveryPhraseSalt = hashPhrase.Item2;
        user.RecoveryPhraseLastViewed = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return new RecoveryPhraseDTO(words, user.RecoveryPhraseLastViewed.Value);
    }

    public async Task<bool> VerifyRecoveryPhraseAsync(string userId, IReadOnlyList<string> words)
    {
        if (words == null || words.Count != WordCount)
            return false;

        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null
            || string.IsNullOrEmpty(user.RecoveryPhraseHash)
            || string.IsNullOrEmpty(user.RecoveryPhraseSalt))
            return false;

        byte[] expectedHash;
        byte[] salt;

        try
        {
            expectedHash = Convert.FromBase64String(user.RecoveryPhraseHash);
            salt = Convert.FromBase64String(user.RecoveryPhraseSalt);
        }
        catch (FormatException)
        {
            // Legacy AES-encrypted blob from before the migration — no longer verifiable.
            return false;
        }

        byte[] actualHash = DeriveKey(NormalizePhrase(words), salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public async Task<DateTime?> GetLastViewedAsync(string userId)
    {
        User? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        return user?.RecoveryPhraseLastViewed;
    }

    // Constant-cost dummy hash used by callers that need to mask whether
    // a given user exists (e.g., the unauthenticated Recover endpoint).
    public void PerformDummyVerify()
    {
        _ = DeriveKey("dummy-recovery-phrase-input", _dummySalt);
    }

    private static readonly byte[] _dummySalt = new byte[SaltSizeBytes];

    private static IReadOnlyList<string> GenerateRandomWords(int count)
    {
        string[] words = new string[count];
        for (int i = 0; i < count; i++)
        {
            int index = RandomNumberGenerator.GetInt32(Bip39Words.All.Length);
            words[i] = Bip39Words.All[index];
        }
        return words;
    }

    private static (string Hash, string Salt) HashPhrase(IReadOnlyList<string> words)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hash = DeriveKey(NormalizePhrase(words), salt);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static byte[] DeriveKey(string phrase, byte[] salt) =>
        KeyDerivation.Pbkdf2(
            password: phrase,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Pbkdf2Iterations,
            numBytesRequested: DerivedKeySizeBytes);

    private static string NormalizePhrase(IReadOnlyList<string> words) =>
        string.Join(' ', words.Select(w => w.Trim().ToLowerInvariant()));
}
