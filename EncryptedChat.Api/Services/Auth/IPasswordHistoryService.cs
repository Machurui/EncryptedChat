using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IPasswordHistoryService
{
    // Returns true if `candidatePlaintext` matches the current password hash or any
    // of the user's last (RetainCount - 1) historical hashes.
    Task<bool> IsReusedAsync(User user, string candidatePlaintext);

    // Records `hashToRecord` (the soon-to-be-replaced current hash) into the
    // history table and prunes anything beyond RetainCount - 1 historical entries.
    // Call this AFTER the new password has been validated but BEFORE replacing
    // user.PasswordHash, with the OLD hash as the argument.
    Task RecordAsync(string userId, string hashToRecord);
}
