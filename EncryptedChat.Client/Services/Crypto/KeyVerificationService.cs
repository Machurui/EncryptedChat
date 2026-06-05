namespace EncryptedChat.Client.Services.Crypto;

public interface IKeyVerificationService
{
    Task<KeyPinResult> CheckAndPinAsync(string userId, string signingPubB64, string encryptionPubB64);
    Task<KeyPinStatus?> GetStatusAsync(string userId);
    Task MarkVerifiedAsync(string userId);
    Task TrustNewKeyAsync(string userId, string signingPubB64, string encryptionPubB64);
    string ComputeSafetyNumber(string userId, string signingPubB64, string encryptionPubB64);
}

public class KeyVerificationService(IPinStore store) : IKeyVerificationService
{
    private readonly IPinStore _store = store;

    public string ComputeSafetyNumber(string userId, string signingPubB64, string encryptionPubB64) =>
        SafetyNumber.Compute(userId, signingPubB64, encryptionPubB64);

    public async Task<KeyPinResult> CheckAndPinAsync(string userId, string signingPubB64, string encryptionPubB64)
    {
        var pin = await _store.GetAsync(userId);
        if (pin == null)
        {
            await _store.SetAsync(userId, new PinRecord(
                signingPubB64, encryptionPubB64,
                ComputeSafetyNumber(userId, signingPubB64, encryptionPubB64),
                "pinned", DateTime.UtcNow.ToString("O"), null));
            return KeyPinResult.Pinned;
        }

        bool same = pin.Sign == signingPubB64 && pin.Enc == encryptionPubB64;
        return same ? KeyPinResult.Matches : KeyPinResult.Changed;
    }

    public async Task<KeyPinStatus?> GetStatusAsync(string userId)
    {
        var pin = await _store.GetAsync(userId);
        return pin == null ? null : new KeyPinStatus(userId, pin.Fingerprint, pin.Status, false);
    }

    public async Task MarkVerifiedAsync(string userId)
    {
        var pin = await _store.GetAsync(userId);
        if (pin == null) return;
        await _store.SetAsync(userId, pin with { Status = "verified", VerifiedAt = DateTime.UtcNow.ToString("O") });
    }

    public async Task TrustNewKeyAsync(string userId, string signingPubB64, string encryptionPubB64)
    {
        await _store.SetAsync(userId, new PinRecord(
            signingPubB64, encryptionPubB64,
            ComputeSafetyNumber(userId, signingPubB64, encryptionPubB64),
            "pinned", DateTime.UtcNow.ToString("O"), null));
    }
}
