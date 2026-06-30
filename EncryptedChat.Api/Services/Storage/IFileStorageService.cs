namespace EncryptedChat.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(byte[] encryptedContent, Guid teamId);
    Task<byte[]> LoadAsync(string storagePath);
    Task DeleteAsync(string storagePath);
    string GetFullPath(string storagePath);

    Task<int> DeleteOrphansAsync(ISet<string> knownStoragePaths, DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
