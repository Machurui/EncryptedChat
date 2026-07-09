using static EncryptedChat.Client.Services.AuthClient;
using static EncryptedChat.Client.Services.Crypto.KeyVaultService;

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
        AlreadyBootstrapped,
        UnwrappedFromServer,
        FreshGeneratedAndUploaded,
        WrongPhrase,
        ServerError
    }

    public async Task<BootstrapOutcome> BootstrapAsync(string userId, string? phraseIfPrompted)
    {
        if (await _vault.IsBootstrappedAsync(userId))
            return BootstrapOutcome.AlreadyBootstrapped;

        if (string.IsNullOrWhiteSpace(phraseIfPrompted))
            throw new InvalidOperationException("Phrase required for bootstrap");

        EncryptionKeysResponse? existing = await _auth.GetMyEncryptionKeysAsync();

        if (existing != null
            && !string.IsNullOrEmpty(existing.EncryptedKeyBundle)
            && !string.IsNullOrEmpty(existing.KeyBundleSalt))
        {
            byte[] salt = Convert.FromBase64String(existing.KeyBundleSalt);
            byte[] bundle = Convert.FromBase64String(existing.EncryptedKeyBundle);
            byte[] wrapKey = await _crypto.DeriveWrapKeyAsync(phraseIfPrompted, salt);

            try
            {
                (byte[] signingPriv, byte[] encPriv) = await _crypto.UnwrapIdentityPrivateKeysAsync(bundle, wrapKey);
                await _vault.StoreMyKeysAsync(userId, signingPriv, encPriv);
                return BootstrapOutcome.UnwrappedFromServer;
            }
            catch (Exception)
            {
                return BootstrapOutcome.WrongPhrase;
            }
        }

        // Server has nothing (post-migration legacy user). Generate fresh.
        CryptoService.IdentityKeyPair pair = await _crypto.GenerateIdentityKeyPairAsync();
        byte[] newSalt = await _crypto.GenerateSaltAsync();
        byte[] wrap = await _crypto.DeriveWrapKeyAsync(phraseIfPrompted, newSalt);
        byte[] wrappedBundle = await _crypto.WrapIdentityPrivateKeysAsync(pair.SigningPrivateKey, pair.EncryptionPrivateKey, wrap);

        bool ok = await _auth.SetEncryptionKeysAsync(
            signingPublicKey: Convert.ToBase64String(pair.SigningPublicKey),
            encryptionPublicKey: Convert.ToBase64String(pair.EncryptionPublicKey),
            encryptedKeyBundle: Convert.ToBase64String(wrappedBundle),
            keyBundleSalt: Convert.ToBase64String(newSalt));

        if (!ok) return BootstrapOutcome.ServerError;

        await _vault.StoreMyKeysAsync(userId, pair.SigningPrivateKey, pair.EncryptionPrivateKey);
        return BootstrapOutcome.FreshGeneratedAndUploaded;
    }

    public async Task<bool> ReWrapAsync(string userId, string newPhrase)
    {
        StoredKeys? stored = await _vault.GetMyKeysAsync(userId);
        if (stored == null) return false;

        byte[] newSalt = await _crypto.GenerateSaltAsync();
        byte[] wrap = await _crypto.DeriveWrapKeyAsync(newPhrase, newSalt);
        byte[] wrappedBundle = await _crypto.WrapIdentityPrivateKeysAsync(stored.SigningPrivateKey, stored.EncryptionPrivateKey, wrap);

        EncryptionKeysResponse? existing = await _auth.GetMyEncryptionKeysAsync();
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

    public async Task<bool> SignupBootstrapAsync(string userId, string phrase)
    {
        CryptoService.IdentityKeyPair pair = await _crypto.GenerateIdentityKeyPairAsync();
        byte[] salt = await _crypto.GenerateSaltAsync();
        byte[] wrap = await _crypto.DeriveWrapKeyAsync(phrase, salt);
        byte[] wrappedBundle = await _crypto.WrapIdentityPrivateKeysAsync(pair.SigningPrivateKey, pair.EncryptionPrivateKey, wrap);

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
