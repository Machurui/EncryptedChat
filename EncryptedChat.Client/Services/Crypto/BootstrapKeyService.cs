namespace EncryptedChat.Client.Services.Crypto;

public class BootstrapKeyService(
    CryptoService crypto,
    KeyVaultService vault,
    AuthClient auth)
{
    private readonly CryptoService _crypto = crypto;
    private readonly KeyVaultService _vault = vault;
    private readonly AuthClient _auth = auth;

    public enum BootstrapOutcome
    {
        AlreadyBootstrapped,          // keys already in IndexedDB; no-op
        UnwrappedFromServer,          // server had a bundle; phrase decrypted it
        FreshGeneratedAndUploaded,    // post-migration legacy user; generated + uploaded
        WrongPhrase,                  // unwrap failed; user should retry
        ServerError                   // PUT failed
    }

    // Called after login. If keys exist in IndexedDB, no-op. If server has a bundle,
    // try to unwrap with the given phrase. If neither, generate fresh keys.
    public async Task<BootstrapOutcome> BootstrapAsync(string userId, string? phraseIfPrompted)
    {
        if (await _vault.IsBootstrappedAsync(userId))
            return BootstrapOutcome.AlreadyBootstrapped;

        if (string.IsNullOrWhiteSpace(phraseIfPrompted))
            throw new InvalidOperationException("Phrase required for bootstrap");

        var existing = await _auth.GetMyEncryptionKeysAsync();

        if (existing != null
            && !string.IsNullOrEmpty(existing.EncryptedKeyBundle)
            && !string.IsNullOrEmpty(existing.KeyBundleSalt))
        {
            byte[] salt = Convert.FromBase64String(existing.KeyBundleSalt);
            byte[] bundle = Convert.FromBase64String(existing.EncryptedKeyBundle);
            byte[] wrapKey = _crypto.DeriveWrapKey(phraseIfPrompted, salt);

            try
            {
                var (signingPriv, encPriv) = _crypto.UnwrapIdentityPrivateKeys(bundle, wrapKey);
                await _vault.StoreMyKeysAsync(userId, signingPriv, encPriv);
                return BootstrapOutcome.UnwrappedFromServer;
            }
            catch (Exception)
            {
                return BootstrapOutcome.WrongPhrase;
            }
        }

        // Server has nothing (post-migration legacy user). Generate fresh.
        CryptoService.IdentityKeyPair pair = _crypto.GenerateIdentityKeyPair();
        byte[] newSalt = _crypto.GenerateSalt();
        byte[] wrap = _crypto.DeriveWrapKey(phraseIfPrompted, newSalt);
        byte[] wrappedBundle = _crypto.WrapIdentityPrivateKeys(pair.SigningPrivateKey, pair.EncryptionPrivateKey, wrap);

        bool ok = await _auth.SetEncryptionKeysAsync(
            signingPublicKey: Convert.ToBase64String(pair.SigningPublicKey),
            encryptionPublicKey: Convert.ToBase64String(pair.EncryptionPublicKey),
            encryptedKeyBundle: Convert.ToBase64String(wrappedBundle),
            keyBundleSalt: Convert.ToBase64String(newSalt));

        if (!ok) return BootstrapOutcome.ServerError;

        await _vault.StoreMyKeysAsync(userId, pair.SigningPrivateKey, pair.EncryptionPrivateKey);
        return BootstrapOutcome.FreshGeneratedAndUploaded;
    }

    // Re-wrap existing private keys with a fresh phrase-derived key.
    // Used by recovery flow (new phrase from server) and regenerate flow.
    public async Task<bool> ReWrapAsync(string userId, string newPhrase)
    {
        var stored = await _vault.GetMyKeysAsync(userId);
        if (stored == null) return false;

        byte[] newSalt = _crypto.GenerateSalt();
        byte[] wrap = _crypto.DeriveWrapKey(newPhrase, newSalt);
        byte[] wrappedBundle = _crypto.WrapIdentityPrivateKeys(stored.SigningPrivateKey, stored.EncryptionPrivateKey, wrap);

        var existing = await _auth.GetMyEncryptionKeysAsync();
        if (existing == null
            || string.IsNullOrEmpty(existing.SigningPublicKey)
            || string.IsNullOrEmpty(existing.EncryptionPublicKey))
        {
            return false;
        }

        return await _auth.SetEncryptionKeysAsync(
            signingPublicKey: existing.SigningPublicKey,
            encryptionPublicKey: existing.EncryptionPublicKey,
            encryptedKeyBundle: Convert.ToBase64String(wrappedBundle),
            keyBundleSalt: Convert.ToBase64String(newSalt));
    }

    // Called at signup, after the server has returned the recovery words.
    // The user is already authed (server issued accessToken). Generate keys, wrap, upload, store.
    public async Task<bool> SignupBootstrapAsync(string userId, string phrase)
    {
        CryptoService.IdentityKeyPair pair = _crypto.GenerateIdentityKeyPair();
        byte[] salt = _crypto.GenerateSalt();
        byte[] wrap = _crypto.DeriveWrapKey(phrase, salt);
        byte[] wrappedBundle = _crypto.WrapIdentityPrivateKeys(pair.SigningPrivateKey, pair.EncryptionPrivateKey, wrap);

        bool ok = await _auth.SetEncryptionKeysAsync(
            signingPublicKey: Convert.ToBase64String(pair.SigningPublicKey),
            encryptionPublicKey: Convert.ToBase64String(pair.EncryptionPublicKey),
            encryptedKeyBundle: Convert.ToBase64String(wrappedBundle),
            keyBundleSalt: Convert.ToBase64String(salt));

        if (!ok) return false;

        await _vault.StoreMyKeysAsync(userId, pair.SigningPrivateKey, pair.EncryptionPrivateKey);
        return true;
    }
}
