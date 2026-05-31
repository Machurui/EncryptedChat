using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IAttachmentService
{
    Task<(AttachmentDTOPublic? Attachment, string? Error, bool IsForbidden)> CreateAsync(
        Guid messageId, AttachmentUploadDTO upload, string userId);
    Task<AttachmentDTOPublic?> GetByIdAsync(Guid id, string userId);
    Task<IReadOnlyList<AttachmentDTOPublic>> GetByMessageIdAsync(Guid messageId, string userId);
    Task<AttachmentDownloadDTO?> DownloadAsync(Guid id, string userId);
    Task<bool> DeleteAsync(Guid id, string userId);
    Task<bool> CanAccessMessageAsync(Guid messageId, string userId);
    Task<Guid?> GetTeamIdForMessageAsync(Guid messageId);
}

// Carries the encrypted blob + envelope produced by the client. The
// server stores everything verbatim and never decrypts. Filename is
// already AES-GCM encrypted on the client; we never see the cleartext
// name.
public class AttachmentUploadDTO
{
    public byte[] EncryptedContent { get; set; } = [];
    public string EncryptedFileName { get; set; } = string.Empty;
    public string FileNameIv { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string FileIv { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int KeyGeneration { get; set; }
}

// Returned to the client when downloading an attachment. EncryptedContent
// is the raw ciphertext (AES-GCM ciphertext || tag) — client decrypts
// using the team key for the matching KeyGeneration.
public class AttachmentDownloadDTO
{
    public byte[] EncryptedContent { get; set; } = [];
    public string EncryptedFileName { get; set; } = string.Empty;
    public string FileNameIv { get; set; } = string.Empty;
    public string FileIv { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public int KeyGeneration { get; set; }
}
