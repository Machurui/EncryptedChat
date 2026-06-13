namespace EncryptedChat.Models;

public record GifVaultReadDTO(string WrappedKey, string Iv, string Blob, int Revision);
