using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EncryptedChat.Services;

// True E2E: the server never decrypts the file content or the filename.
// We validate membership + size + declared MIME type, store the cipher
// blob to disk verbatim, and persist the envelope alongside it.
//
// Note: the magic-byte content sniff that lived here under server-side
// crypto is intentionally gone — it can't run against ciphertext. Clients
// are responsible for filename / extension trust decisions after they
// decrypt the envelope, and the rendering surface is responsible for
// sandboxing untrusted content (e.g. images via <img>, never <script>).
public class AttachmentService(
    EncryptedChatContext context,
    IFileStorageService storage,
    MimeTypeValidator validator,
    IOptions<FileStorageOptions> options) : IAttachmentService
{
    private readonly EncryptedChatContext _context = context;
    private readonly IFileStorageService _storage = storage;
    private readonly MimeTypeValidator _validator = validator;
    private readonly long _maxFileSize = options.Value.MaxFileSizeBytes;

    public async Task<(AttachmentDTOPublic? Attachment, string? Error, bool IsForbidden)> CreateAsync(
        Guid messageId, AttachmentUploadDTO upload, string userId)
    {
        if (upload == null || upload.EncryptedContent.Length == 0)
            return (null, "Le fichier est vide", false);

        if (upload.EncryptedContent.Length > _maxFileSize)
            return (null, $"Fichier trop volumineux ({upload.EncryptedContent.Length / 1_048_576} Mo). Limite : {_maxFileSize / 1_048_576} Mo", false);

        if (string.IsNullOrWhiteSpace(upload.EncryptedFileName)
            || string.IsNullOrWhiteSpace(upload.FileNameIv)
            || string.IsNullOrWhiteSpace(upload.FileIv)
            || string.IsNullOrWhiteSpace(upload.Signature)
            || string.IsNullOrWhiteSpace(upload.MimeType))
            return (null, "Enveloppe incomplète", false);

        // Declared-MIME allow-list. We can't sniff content (it's
        // ciphertext) so this is the only MIME gate left. Clients should
        // refuse to render anything they can't match against their own
        // post-decryption sniff.
        if (!_validator.IsDeclaredMimeTypeAllowed(upload.MimeType))
            return (null, $"Type MIME '{upload.MimeType}' non autorisé", false);

        Message? message = await GetMessageWithTeamAsync(messageId);
        if (message?.Team == null || message.Sender == null)
            return (null, "Message introuvable", false);

        if (!await IsMemberAsync(message.Team.Id, userId))
            return (null, "Accès non autorisé", true);

        // E2E invariant: attachments must be encrypted under the team's
        // current generation, same as messages.
        if (upload.KeyGeneration != message.Team.KeyGeneration)
            return (null, "Generation de clé invalide", false);

        string storagePath = await _storage.SaveAsync(upload.EncryptedContent, message.Team.Id);

        Attachment attachment = new()
        {
            MessageId = messageId,
            EncryptedFileName = upload.EncryptedFileName,
            FileNameIv = upload.FileNameIv,
            MimeType = upload.MimeType,
            Size = upload.EncryptedContent.Length,
            StoragePath = storagePath,
            FileIv = upload.FileIv,
            Signature = upload.Signature,
            KeyGeneration = upload.KeyGeneration,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.Attachments.Add(attachment);
            await _context.SaveChangesAsync();
        }
        catch
        {
            await _storage.DeleteAsync(storagePath);
            throw;
        }

        return (ToDTO(attachment), null, false);
    }

    public async Task<AttachmentDTOPublic?> GetByIdAsync(Guid id, string userId)
    {
        Attachment? attachment = await GetAttachmentWithRelationsAsync(id);
        if (attachment?.Message?.Team == null)
            return null;

        if (!await IsMemberAsync(attachment.Message.Team.Id, userId))
            return null;

        return ToDTO(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDTOPublic>> GetByMessageIdAsync(Guid messageId, string userId)
    {
        Message? message = await GetMessageWithTeamAsync(messageId);
        if (message?.Team == null || !await IsMemberAsync(message.Team.Id, userId))
            return [];

        List<Attachment> attachments = await _context.Attachments
            .Include(a => a.Message).ThenInclude(m => m!.Team)
            .AsNoTracking()
            .Where(a => a.MessageId == messageId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        return attachments.Select(ToDTO).ToList();
    }

    public async Task<bool> CanAccessMessageAsync(Guid messageId, string userId)
    {
        Message? message = await GetMessageWithTeamAsync(messageId);
        return message?.Team != null && await IsMemberAsync(message.Team.Id, userId);
    }

    public async Task<AttachmentDownloadDTO?> DownloadAsync(Guid id, string userId)
    {
        Attachment? attachment = await _context.Attachments
            .Include(a => a.Message).ThenInclude(m => m!.Team)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment?.Message?.Team == null || !await IsMemberAsync(attachment.Message.Team.Id, userId))
            return null;

        byte[] encryptedContent = await _storage.LoadAsync(attachment.StoragePath);

        return new AttachmentDownloadDTO
        {
            EncryptedContent = encryptedContent,
            EncryptedFileName = attachment.EncryptedFileName,
            FileNameIv = attachment.FileNameIv,
            FileIv = attachment.FileIv,
            Signature = attachment.Signature,
            MimeType = attachment.MimeType,
            KeyGeneration = attachment.KeyGeneration
        };
    }

    public async Task<bool> DeleteAsync(Guid id, string userId)
    {
        Attachment? attachment = await _context.Attachments
            .Include(a => a.Message).ThenInclude(m => m!.Team)
            .Include(a => a.Message).ThenInclude(m => m!.Sender)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment?.Message?.Team == null)
            return false;

        bool isOwner = attachment.Message.Sender?.Id == userId;
        bool isAdmin = await _context.Members
            .AnyAsync(m => m.TeamId == attachment.Message.Team.Id && m.UserId == userId
                           && (m.Role == Member.AdminRole || m.Role == Member.OwnerRole));

        if (!isOwner && !isAdmin)
            return false;

        string storagePath = attachment.StoragePath;

        _context.Attachments.Remove(attachment);
        await _context.SaveChangesAsync();

        try
        {
            await _storage.DeleteAsync(storagePath);
        }
        catch (IOException)
        {
            // Orphan file - preferable to dangling DB reference
        }

        return true;
    }

    public async Task<Guid?> GetTeamIdForMessageAsync(Guid messageId)
    {
        Message? message = await _context.Messages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new Message { Team = m.Team })
            .FirstOrDefaultAsync();
        return message?.Team?.Id;
    }

    private Task<bool> IsMemberAsync(Guid teamId, string userId) =>
        _context.Members.AnyAsync(m => m.TeamId == teamId && m.UserId == userId);

    private Task<Message?> GetMessageWithTeamAsync(Guid messageId) =>
        _context.Messages
            .Include(m => m.Team)
            .Include(m => m.Sender)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId);

    private Task<Attachment?> GetAttachmentWithRelationsAsync(Guid id) =>
        _context.Attachments
            .Include(a => a.Message).ThenInclude(m => m!.Team)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

    private static AttachmentDTOPublic ToDTO(Attachment attachment) => new()
    {
        Id = attachment.Id,
        MessageId = attachment.MessageId,
        EncryptedFileName = attachment.EncryptedFileName,
        FileNameIv = attachment.FileNameIv,
        MimeType = attachment.MimeType,
        Size = attachment.Size,
        FileIv = attachment.FileIv,
        Signature = attachment.Signature,
        KeyGeneration = attachment.KeyGeneration,
        CreatedAt = attachment.CreatedAt
    };
}
