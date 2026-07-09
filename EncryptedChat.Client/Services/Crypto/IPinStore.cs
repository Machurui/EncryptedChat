namespace EncryptedChat.Client.Services.Crypto;

public enum KeyPinResult { Pinned, Matches, Changed }

public record PinRecord(
    string Sign,
    string Enc,
    string Fingerprint,
    string Status,
    string PinnedAt,
    string? VerifiedAt);

public record KeyPinStatus(string UserId, string Fingerprint, string Status, bool Changed);

public interface IPinStore
{
    Task<PinRecord?> GetAsync(string userId);
    Task SetAsync(string userId, PinRecord record);
}
