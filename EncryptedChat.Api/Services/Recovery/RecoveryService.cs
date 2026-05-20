using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncryptedChat.Services;

public class RecoveryService(EncryptedChatContext context, UserManager<User> userManager, IConfiguration configuration) : IRecoveryService
{
    private readonly EncryptedChatContext _context = context;
    private readonly UserManager<User> _userManager = userManager;
    private readonly byte[] _encryptionKey = DeriveKey(configuration["Jwt:Key"] ?? "default-recovery-key-at-least-32-bytes!");

    private static readonly string[] WordList =
    [
        "anchor", "beacon", "cipher", "dawn", "ember", "frost", "glade", "helix", "iron", "jade",
        "knot", "lumen", "mist", "noble", "orbit", "prism", "quartz", "ridge", "stone", "torch",
        "unity", "vault", "wave", "xenon", "yield", "zenith", "alpha", "bravo", "coral", "delta",
        "echo", "flame", "gamma", "haven", "index", "jewel", "kappa", "lunar", "maple", "north",
        "ocean", "pixel", "quest", "river", "solar", "titan", "ultra", "vivid", "winter", "xray",
        "yacht", "zephyr", "amber", "blaze", "crown", "drift", "epoch", "forge", "grain", "haste",
        "ivory", "joker", "karma", "light", "marsh", "nexus", "onyx", "pearl", "quiet", "realm",
        "shade", "trend", "umbra", "verse", "whirl", "xerox", "youth", "zodiac", "atlas", "bloom",
        "crest", "dune", "edge", "fable", "gleam", "honor", "inlet", "joint", "keeper", "lance",
        "mount", "nerve", "oasis", "patch", "quill", "roost", "spark", "trace", "union", "vigor",
        "woven", "axiom", "basil", "chime", "drake", "elite", "flora", "grace", "hinge", "ideal",
        "jazzy", "knack", "lotus", "medal", "novel", "optic", "plume", "quote", "rapid", "scope",
        "thorn", "usage", "valid", "wager", "xenial", "yearn", "zonal"
    ];

    public async Task<RecoveryPhraseDTO?> GenerateRecoveryPhraseAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var words = GenerateRandomWords(12);
        var phraseJson = JsonSerializer.Serialize(words);

        user.RecoveryPhraseHash = EncryptPhrase(phraseJson);
        user.RecoveryPhraseLastViewed = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        return new RecoveryPhraseDTO(words, DateTime.UtcNow);
    }

    public async Task<RecoveryPhraseDTO?> GetRecoveryPhraseAsync(string userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var isValidPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!isValidPassword)
            return null;

        if (string.IsNullOrEmpty(user.RecoveryPhraseHash))
        {
            return await GenerateRecoveryPhraseAsync(userId);
        }

        var words = DecryptPhrase(user.RecoveryPhraseHash);
        if (words == null)
        {
            return await GenerateRecoveryPhraseAsync(userId);
        }

        user.RecoveryPhraseLastViewed = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return new RecoveryPhraseDTO(words, user.RecoveryPhraseLastViewed ?? DateTime.UtcNow);
    }

    public async Task<bool> VerifyRecoveryPhraseAsync(string userId, IReadOnlyList<string> words)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || string.IsNullOrEmpty(user.RecoveryPhraseHash))
            return false;

        var storedWords = DecryptPhrase(user.RecoveryPhraseHash);
        if (storedWords == null)
            return false;

        var normalizedInput = words.Select(w => w.ToLowerInvariant()).ToList();
        var normalizedStored = storedWords.Select(w => w.ToLowerInvariant()).ToList();

        return normalizedInput.SequenceEqual(normalizedStored);
    }

    public async Task<DateTime?> GetLastViewedAsync(string userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        return user?.RecoveryPhraseLastViewed;
    }

    public async Task UpdateLastViewedAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.RecoveryPhraseLastViewed = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
    }

    private static IReadOnlyList<string> GenerateRandomWords(int count)
    {
        var words = new List<string>(count);
        var usedIndices = new HashSet<int>();

        while (words.Count < count)
        {
            var index = RandomNumberGenerator.GetInt32(WordList.Length);
            if (usedIndices.Add(index))
            {
                words.Add(WordList[index]);
            }
        }

        return words;
    }

    private static byte[] DeriveKey(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    private string EncryptPhrase(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    private IReadOnlyList<string>? DecryptPhrase(string encrypted)
    {
        try
        {
            var data = Convert.FromBase64String(encrypted);
            if (data.Length < 17) return null;

            using var aes = Aes.Create();
            aes.Key = _encryptionKey;

            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            aes.IV = iv;

            var ciphertext = new byte[data.Length - 16];
            Buffer.BlockCopy(data, 16, ciphertext, 0, ciphertext.Length);

            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            var plaintext = Encoding.UTF8.GetString(plaintextBytes);

            return JsonSerializer.Deserialize<List<string>>(plaintext);
        }
        catch
        {
            return null;
        }
    }
}
