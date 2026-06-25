using Microsoft.Extensions.Options;

namespace EncryptedChat.Services;

public class FileStorageOptions
{
    public string BasePath { get; set; } = "./storage/attachments";
    public long MaxFileSizeBytes { get; set; } = 26_214_400;
    public long MaxTeamStorageBytes { get; set; } = 1_073_741_824; // 1 GiB per team
    public string[] AllowedExtensions { get; set; } =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg",
        ".pdf", ".txt", ".csv", ".md",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".mp3", ".wav", ".ogg", ".m4a", ".flac",
        ".mp4", ".webm", ".mov", ".avi", ".mkv",
        ".json", ".xml", ".html", ".css", ".js", ".ts", ".py", ".java", ".cs", ".cpp", ".c", ".h"
    ];
}

public class FileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    private readonly string _basePath = Path.GetFullPath(options.Value.BasePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public async Task<string> SaveAsync(byte[] encryptedContent, Guid teamId)
    {
        EnsureBasePathExists();
        string teamDir = Path.Combine(_basePath, teamId.ToString());
        Directory.CreateDirectory(teamDir);

        string relativePath = Path.Combine(teamId.ToString(), $"{Guid.NewGuid()}.enc");
        string fullPath = Path.Combine(_basePath, relativePath);
        ValidatePath(fullPath);

        await File.WriteAllBytesAsync(fullPath, encryptedContent);
        return relativePath;
    }

    public async Task<byte[]> LoadAsync(string storagePath)
    {
        string fullPath = GetFullPath(storagePath);
        ValidatePath(fullPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Attachment file not found", storagePath);

        return await File.ReadAllBytesAsync(fullPath);
    }

    public Task DeleteAsync(string storagePath)
    {
        string fullPath = GetFullPath(storagePath);
        ValidatePath(fullPath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    public string GetFullPath(string storagePath) => Path.Combine(_basePath, storagePath);

    public Task<int> DeleteOrphansAsync(ISet<string> knownStoragePaths, DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_basePath))
            return Task.FromResult(0);

        int deleted = 0;
        foreach (string fullPath in Directory.EnumerateFiles(_basePath, "*.enc", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(_basePath, fullPath);
            if (knownStoragePaths.Contains(relativePath))
                continue;

            // An upload writes the blob before committing its DB row, so a fresh
            // "unknown" file may just be an in-flight upload — leave recent files alone.
            if (File.GetLastWriteTimeUtc(fullPath) >= cutoffUtc)
                continue;

            try
            {
                File.Delete(fullPath);
                deleted++;
            }
            catch (IOException)
            {
                // Locked/transient — retried on the next sweep.
            }
        }

        return Task.FromResult(deleted);
    }

    private void EnsureBasePathExists() => Directory.CreateDirectory(_basePath);

    private void ValidatePath(string fullPath)
    {
        string canonical = Path.GetFullPath(fullPath);
        if (!canonical.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Invalid storage path");

        string relativePath = Path.GetRelativePath(_basePath, canonical);
        if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath))
            throw new UnauthorizedAccessException("Invalid storage path");
    }
}
