using Microsoft.Extensions.Options;

namespace EncryptedChat.Services;

public class MimeTypeValidator(IOptions<FileStorageOptions> options)
{
    private readonly HashSet<string> _allowedExtensions = new(
        options.Value.AllowedExtensions.Select(e => e.ToLowerInvariant()));

    private static readonly Dictionary<string, string[]> MimeTypesByCategory = new()
    {
        ["image"] = ["image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml"],
        ["document"] = ["application/pdf", "text/plain", "text/csv", "text/markdown",
            "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.oasis.opendocument.text", "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.presentation"],
        ["archive"] = ["application/zip", "application/x-rar-compressed", "application/x-7z-compressed",
            "application/gzip", "application/x-tar"],
        ["audio"] = ["audio/mpeg", "audio/wav", "audio/ogg", "audio/mp4", "audio/flac", "audio/x-m4a"],
        ["video"] = ["video/mp4", "video/webm", "video/quicktime", "video/x-msvideo", "video/x-matroska"],
        ["code"] = ["application/json", "application/xml", "text/html", "text/css", "text/javascript",
            "application/javascript", "text/x-python", "text/x-java-source", "text/x-csharp"]
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(
        MimeTypesByCategory.Values.SelectMany(m => m));

    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        ["image/gif"] = [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],
        ["image/webp"] = [[0x52, 0x49, 0x46, 0x46]],
        ["image/bmp"] = [[0x42, 0x4D]],
        ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]],
        ["application/zip"] = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06], [0x50, 0x4B, 0x07, 0x08]],
        ["application/x-rar-compressed"] = [[0x52, 0x61, 0x72, 0x21, 0x1A, 0x07]],
        ["application/gzip"] = [[0x1F, 0x8B]],
        ["application/x-7z-compressed"] = [[0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]],
        ["video/mp4"] = [[0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], [0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70]],
        ["video/webm"] = [[0x1A, 0x45, 0xDF, 0xA3]],
        ["audio/mpeg"] = [[0xFF, 0xFB], [0xFF, 0xFA], [0xFF, 0xF3], [0xFF, 0xF2], [0x49, 0x44, 0x33]],
        ["audio/wav"] = [[0x52, 0x49, 0x46, 0x46]],
        ["audio/ogg"] = [[0x4F, 0x67, 0x67, 0x53]],
        ["audio/flac"] = [[0x66, 0x4C, 0x61, 0x43]],
    };

    private static readonly HashSet<string> DangerousMimeTypes =
    [
        "application/x-msdownload", "application/x-executable", "application/x-dosexec",
        "application/x-msdos-program", "application/bat", "application/x-bat",
        "application/x-msi", "application/vnd.microsoft.portable-executable"
    ];

    public FileValidationResult Validate(byte[] content, string declaredMimeType, string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
        {
            string allowed = string.Join(", ", _allowedExtensions.Take(10));
            return FileValidationResult.Failure(
                FileValidationError.ExtensionNotAllowed,
                $"Extension '{extension}' non autorisée. Extensions acceptées : {allowed}...");
        }

        if (!AllowedMimeTypes.Contains(declaredMimeType.ToLowerInvariant()) &&
            !declaredMimeType.StartsWith("text/"))
        {
            return FileValidationResult.Failure(
                FileValidationError.MimeTypeNotAllowed,
                $"Type MIME '{declaredMimeType}' non autorisé");
        }

        string? detectedMimeType = DetectMimeType(content);

        if (detectedMimeType != null && DangerousMimeTypes.Contains(detectedMimeType))
        {
            return FileValidationResult.Failure(
                FileValidationError.ContentMismatch,
                "Contenu du fichier potentiellement dangereux détecté");
        }

        if (declaredMimeType.StartsWith("image/") && detectedMimeType != null && !detectedMimeType.StartsWith("image/"))
        {
            return FileValidationResult.Failure(
                FileValidationError.ContentMismatch,
                "Le contenu du fichier ne correspond pas au type image déclaré");
        }

        return FileValidationResult.Success();
    }

    public string GetAllowedExtensionsMessage()
    {
        return string.Join(", ", _allowedExtensions.OrderBy(e => e));
    }

    private static string? DetectMimeType(byte[] content)
    {
        if (content.Length < 8)
            return null;

        foreach (KeyValuePair<string, byte[][]> entry in MagicBytes)
        {
            foreach (byte[] signature in entry.Value)
            {
                if (content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature))
                    return entry.Key;
            }
        }
        return null;
    }
}
