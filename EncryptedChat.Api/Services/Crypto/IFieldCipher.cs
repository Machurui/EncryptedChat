namespace EncryptedChat.Services;

// Server-side at-rest field encryption (AES-256-GCM). Null/empty pass through.
// `aad` (e.g. the column name) is bound as GCM associated data so a value encrypted
// for one field cannot be transposed to another. Decrypt tolerates legacy
// (pre-encryption) plaintext by returning it unchanged.
public interface IFieldCipher
{
    string? Encrypt(string? plaintext, string aad);
    string? Decrypt(string? stored, string aad);
}
