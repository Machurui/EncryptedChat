namespace EncryptedChat.Services;

// Deterministic keyed digest for equality lookups on encrypted columns
// (e.g. Handle uniqueness/search). HMAC-SHA256, key derived from Encryption:Key.
// Leaks equality by design (acceptable vs a read-only SQL adversary). Caller
// must normalize the value (trim + lowercase) before calling.
public interface IBlindIndex
{
    string Compute(string normalizedValue);
}
