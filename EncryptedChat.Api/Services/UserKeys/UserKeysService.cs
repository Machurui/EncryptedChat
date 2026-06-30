using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class UserKeysService(EncryptedChatContext context, UserManager<User> userManager) : IUserKeysService
{
    private readonly EncryptedChatContext _context = context;
    private readonly UserManager<User> _userManager = userManager;

    public async Task<EncryptionKeysDTO?> GetMyKeysAsync(string userId)
    {
        User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return null;

        if (string.IsNullOrEmpty(user.SigningPublicKey)
            && string.IsNullOrEmpty(user.EncryptionPublicKey)
            && string.IsNullOrEmpty(user.EncryptedKeyBundle))
            return null;

        return new EncryptionKeysDTO(
            user.SigningPublicKey,
            user.EncryptionPublicKey,
            user.EncryptedKeyBundle,
            user.KeyBundleSalt);
    }

    public async Task<bool> SetMyKeysAsync(string userId, SetEncryptionKeysDTO dto)
    {
        User? user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        user.SigningPublicKey = dto.SigningPublicKey;
        user.EncryptionPublicKey = dto.EncryptionPublicKey;
        user.EncryptedKeyBundle = dto.EncryptedKeyBundle;
        user.KeyBundleSalt = dto.KeyBundleSalt;

        IdentityResult result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<PublicKeysDTO?> GetPublicKeysAsync(string userId)
    {
        User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null
            || string.IsNullOrEmpty(user.SigningPublicKey)
            || string.IsNullOrEmpty(user.EncryptionPublicKey))
            return null;

        return new PublicKeysDTO(user.SigningPublicKey, user.EncryptionPublicKey);
    }
}
