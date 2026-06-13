namespace EncryptedChat.Models;

public record GifVaultWriteDTO(string WrappedKey, string Iv, string Blob, int ExpectedRevision);
