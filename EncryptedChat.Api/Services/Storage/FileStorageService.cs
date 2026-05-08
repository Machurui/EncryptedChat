using Microsoft.Extensions.Options;

namespace EncryptedChat.Services;

public class FileStorageOptions
{
    public string BasePath { get; set; } = "./storage/attachments";
    public long MaxFileSizeBytes { get; set; } = 26_214_400;
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
