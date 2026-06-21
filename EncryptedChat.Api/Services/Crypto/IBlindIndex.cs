namespace EncryptedChat.Services;

// Deterministic keyed digest for equality lookups on encrypted columns.
// HMAC-SHA256, key derived (HKDF) from Encryption:Key, with a distinct sub-key per
// `purpose` (domain separation: "blind-index"=handle, "identity"=email/username, "slug").
// Leaks equality by design (acceptable vs a read-only SQL adversary). Caller normalizes.
public interface IBlindIndex
{
    string Compute(string normalizedValue, string purpose = "blind-index");
}
