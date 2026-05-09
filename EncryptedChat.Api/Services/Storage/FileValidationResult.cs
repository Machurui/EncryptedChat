namespace EncryptedChat.Services;

public class FileValidationResult
{
    public bool IsValid { get; private init; }
    public string? ErrorMessage { get; private init; }
    public FileValidationError? Error { get; private init; }

    public static FileValidationResult Success() => new() { IsValid = true };

    public static FileValidationResult Failure(FileValidationError error, string message) =>
        new() { IsValid = false, Error = error, ErrorMessage = message };
}

public enum FileValidationError
{
    ExtensionNotAllowed,
    MimeTypeNotAllowed,
    ContentMismatch,
    FileTooLarge,
    EmptyFile
}
