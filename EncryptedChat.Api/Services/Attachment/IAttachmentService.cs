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