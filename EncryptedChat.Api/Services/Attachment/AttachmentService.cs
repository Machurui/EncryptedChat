using System.Security.Cryptography;
using System.Text;
using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EncryptedChat.Services;

public class AttachmentService(
    EncryptedChatContext context,
    ICryptoService crypto,
    IFileStorageService storage,
    MimeTypeValidator validator,
    IOptions<FileStorageOptions> options) : IAttachmentService
{
    private readonly EncryptedChatContext _context = context;
    private readonly ICryptoService _crypto = crypto;
    private readonly IFileStorageService _storage = storage;
    private readonly MimeTypeValidator _validator = validator;
    private readonly long _maxFileSize = options.Value.MaxFileSizeBytes;

    public async Task<(AttachmentDTOPublic? Attachment, string? Error, bool IsForbidden)> CreateAsync(
        Guid messageId, string fileName, string mimeType, byte[] content, string userId)
    {
        if (content.Length == 0)
            return (null, "Le fichier est vide", false);

        if (content.Length > _maxFileSize)
            return (null, $"Fichier trop volumineux ({content.Length / 1_048_576} Mo). Limite : {_maxFileSize / 1_048_576} Mo", false);

        FileValidationResult validation = _validator.Validate(content, mimeType, fileName);
        if (!validation.IsValid)
            return (null, validation.ErrorMessage, false);

        Message? message = await GetMessageWithTeamAsync(messageId);
        if (message?.Team == null || message.Sender == null)
            return (null, "Message introuvable", false);

        if (!await IsMemberAsync(message.Team.Id, userId))
            return (null, "Accès non autorisé", true);

        // TEMP-Task3: (byte[] encryptedContent, string fileIv) = _crypto.EncryptBytes(content, message.Team.Secret);
        // TEMP-Task3: (byte[] encryptedFileName, string fileNameIv) = _crypto.EncryptBytes(Encoding.UTF8.GetBytes(fileName), message.Team.Secret);
        byte[] encryptedContent = content;
        string fileIv = string.Empty;
        byte[] encryptedFileName = Encoding.UTF8.GetBytes(fileName);
        string fileNameIv = string.Empty;

        string storagePath = await _storage.SaveAsync(encryptedContent, message.Team.Id);

        Attachment attachment = new()
        {
            MessageId = messageId,
            EncryptedFileName = Convert.ToBase64String(encryptedFileName),
            FileNameIv = fileNameIv,
            MimeType = mimeType,
            Size = content.Length,
            StoragePath = storagePath,
            FileIv = fileIv,
            // TEMP-Task3: Signature = _crypto.SignBytes(content, message.Sender.Secret),
            Signature = string.Empty,
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

        return (ToDTO(attachment, fileName), null, false);
    }

    public async Task<AttachmentDTOPublic?> GetByIdAsync(Guid id, string userId)
    {
        Attachment? attachment = await GetAttachmentWithRelationsAsync(id);
        if (attachment?.Message?.Team == null)
            return null;

        if (!await IsMemberAsync(attachment.Message.Team.Id, userId))
            return null;

        return DecryptMetadata(attachment);
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

        return attachments.Select(DecryptMetadata).ToList();
    }

    public async Task<bool> CanAccessMessageAsync(Guid messageId, string userId)
    {
        Message? message = await GetMessageWithTeamAsync(messageId);
        return message?.Team != null && await IsMemberAsync(message.Team.Id, userId);
    }

    public async Task<(byte[] Content, string FileName, string MimeType)?> DownloadAsync(Guid id, string userId)
    {
        Attachment? attachment = await _context.Attachments
            .Include(a => a.Message).ThenInclude(m => m!.Team)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment?.Message?.Team == null || !await IsMemberAsync(attachment.Message.Team.Id, userId))
            return null;

        // TEMP-Task3: string teamSecret = attachment.Message.Team.Secret;
        // TEMP-Task3: byte[] content = _crypto.DecryptBytes(await _storage.LoadAsync(attachment.StoragePath), attachment.FileIv, teamSecret);
        // TEMP-Task3: string fileName = Encoding.UTF8.GetString(
        // TEMP-Task3:     _crypto.DecryptBytes(Convert.FromBase64String(attachment.EncryptedFileName), attachment.FileNameIv, teamSecret));
        byte[] content = await _storage.LoadAsync(attachment.StoragePath);
        string fileName = attachment.EncryptedFileName;

        return (content, fileName, attachment.MimeType);
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
            .AnyAsync(m => m.TeamId == attachment.Message.Team.Id && m.UserId == userId && m.Role == Member.AdminRole);

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

    private AttachmentDTOPublic DecryptMetadata(Attachment attachment)
    {
        string fileName;

        try
        {
            // TEMP-Task3: string teamSecret = attachment.Message?.Team?.Secret ?? string.Empty;
            // TEMP-Task3: fileName = Encoding.UTF8.GetString(
            // TEMP-Task3:     _crypto.DecryptBytes(Convert.FromBase64String(attachment.EncryptedFileName), attachment.FileNameIv, teamSecret));
            fileName = attachment.EncryptedFileName;
        }
        catch (CryptographicException) { fileName = "[Decryption failed]"; }
        catch (FormatException) { fileName = "[Invalid format]"; }

        return ToDTO(attachment, fileName);
    }

    private static AttachmentDTOPublic ToDTO(Attachment attachment, string fileName) => new()
    {
        Id = attachment.Id,
        MessageId = attachment.MessageId,
        FileName = fileName,
        MimeType = attachment.MimeType,
        Size = attachment.Size,
        CreatedAt = attachment.CreatedAt,
        SignatureVerified = false
    };
}
