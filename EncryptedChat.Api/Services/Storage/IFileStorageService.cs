namespace EncryptedChat.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(byte[] encryptedContent, Guid teamId);
    Task<byte[]> LoadAsync(string storagePath);
    Task DeleteAsync(string storagePath);
    string GetFullPath(string storagePath);

    // Reconciles disk against the DB: deletes *.enc blobs whose relative path is not
    // in knownStoragePaths and whose last-write time is older than cutoffUtc (the age
    // guard avoids racing an upload whose row isn't committed yet). Returns the count.
    Task<int> DeleteOrphansAsync(ISet<string> knownStoragePaths, DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
