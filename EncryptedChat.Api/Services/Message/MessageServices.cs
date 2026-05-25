using System.Security.Cryptography;
using System.Text;
using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class MessageService(EncryptedChatContext context, ICryptoService crypto) : IMessageService
{
    private readonly EncryptedChatContext _context = context;
    private readonly ICryptoService _crypto = crypto;

    private const int MaxPageSize = 100;

    public async Task<IReadOnlyList<MessageDTOPublic>?> GetAllByTeamAsync(Guid id, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        Team? team = await _context.Teams.FindAsync(id);
        if (team == null)
            return null;

        List<Message> messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .Include(m => m.Attachments)
            .AsNoTracking()
            .Where(m => m.Team != null && m.Team.Id == team.Id)
            .OrderByDescending(m => m.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.Select(m => DecryptAndMapMessage(m, team.Secret)).ToList();
    }

    public async Task<MessageDTOPublic?> GetByIdAsync(Guid id)
    {
        Message? message = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(m => m.User)
            .Include(m => m.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (message == null)
            return null;

        return DecryptAndMapMessage(message);
    }

    public async Task<MessageDTOPublic?> CreateAsync(MessageDTO message, string senderId)
    {
        if (string.IsNullOrWhiteSpace(senderId))
            return null;

        User? sender = await _context.Users.FindAsync(senderId);
        if (sender == null)
            return null;

        Guid? teamId = message?.Team;
        if (teamId == null)
            return null;

        Team? team = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null)
            return null;

        if (team.Members == null)
            return null;

        bool isMember = team.Members.Any(m => m.UserId == sender.Id);
        if (!isMember)
            return null;

        string text = message?.Text ?? "";
        (string encryptedText, string iv) = _crypto.Encrypt(text, team.Secret);
        string signature = _crypto.Sign(text, sender.Secret);

        Message newMessage = new()
        {
            EncryptedText = encryptedText,
            Iv = iv,
            Signature = signature,
            Sender = sender,
            Team = team,
            Date = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(newMessage);
        await _context.SaveChangesAsync();

        return ItemToDTO(newMessage, message.Text, signatureVerified: true);
    }

    public async Task<MessageDTOPublic?> UpdateAsync(Guid id, MessageDTO message, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        Message? messageToUpdate = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToUpdate == null)
            return null;

        if (messageToUpdate.Sender?.Id != actorId)
            return null;

        if (messageToUpdate.Team == null)
            return null;

        if (string.IsNullOrWhiteSpace(message?.Text))
            return null;

        User? actor = await _context.Users.FindAsync(actorId);
        if (actor == null)
            return null;

        (string encryptedText, string iv) = _crypto.Encrypt(message.Text, messageToUpdate.Team.Secret);
        string signature = _crypto.Sign(message.Text, actor.Secret);

        messageToUpdate.EncryptedText = encryptedText;
        messageToUpdate.Iv = iv;
        messageToUpdate.Signature = signature;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!MessageExists(id))
                return null;
            throw;
        }

        return ItemToDTO(messageToUpdate, message.Text, signatureVerified: true);
    }

    public async Task<MessageDTOPublic?> DeleteAsync(Guid id, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        Message? messageToDelete = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToDelete == null)
            return null;

        bool isOwner = messageToDelete.Sender?.Id == actorId;
        bool isAdmin = messageToDelete.Team != null && await _context.Members
            .AnyAsync(m => m.TeamId == messageToDelete.Team.Id && m.UserId == actorId && m.Role == Member.AdminRole);

        if (!isOwner && !isAdmin)
            return null;

        MessageDTOPublic dto = DecryptAndMapMessage(messageToDelete);

        _context.Messages.Remove(messageToDelete);
        await _context.SaveChangesAsync();

        return dto;
    }

    private bool MessageExists(Guid id)
    {
        return _context.Messages.Any(e => e.Id == id);
    }

    private MessageDTOPublic DecryptAndMapMessage(Message message, string? teamSecret = null)
    {
        string secret = teamSecret ?? message.Team?.Secret ?? string.Empty;
        string plaintext;
        bool signatureVerified = false;

        try
        {
            plaintext = _crypto.Decrypt(message.EncryptedText, message.Iv, secret);
            string senderSecret = message.Sender?.Secret ?? string.Empty;
            signatureVerified = _crypto.Verify(plaintext, message.Signature, senderSecret);
        }
        catch (CryptographicException)
        {
            plaintext = "[Decryption failed]";
        }
        catch (FormatException)
        {
            plaintext = "[Invalid message format]";
        }

        return ItemToDTO(message, plaintext, signatureVerified);
    }

    private MessageDTOPublic ItemToDTO(Message message, string text, bool signatureVerified)
    {
        string teamSecret = message.Team?.Secret ?? string.Empty;

        List<AttachmentDTOPublic> attachments = [];
        if (message.Attachments != null)
        {
            foreach (Attachment attachment in message.Attachments)
            {
                string fileName;
                try
                {
                    byte[] encryptedFileName = Convert.FromBase64String(attachment.EncryptedFileName);
                    byte[] fileNameBytes = _crypto.DecryptBytes(encryptedFileName, attachment.FileNameIv, teamSecret);
                    fileName = Encoding.UTF8.GetString(fileNameBytes);
                }
                catch
                {
                    fileName = "[Decryption failed]";
                }

                attachments.Add(new AttachmentDTOPublic
                {
                    Id = attachment.Id,
                    MessageId = attachment.MessageId,
                    FileName = fileName,
                    MimeType = attachment.MimeType,
                    Size = attachment.Size,
                    CreatedAt = attachment.CreatedAt,
                    SignatureVerified = false // Non vérifié au listing - vérification au téléchargement
                });
            }
        }

        return new MessageDTOPublic
        {
            Id = message.Id,
            Text = text,
            Sender = new MessageSenderDTO
            {
                Id = message.Sender?.Id ?? string.Empty,
                Name = message.Sender?.Name ?? string.Empty,
                Handle = message.Sender?.Handle,
                NameColor = message.Sender?.NameColor ?? "#FFFFFF",
                ProfileImageUrl = message.Sender?.ProfileImageUrl
            },
            TeamId = message.Team?.Id ?? Guid.Empty,
            Date = message.Date,
            SignatureVerified = signatureVerified,
            Attachments = attachments
        };
    }

    public Task<int> CountByTeamAsync(Guid teamId)
    {
        return _context.Messages.CountAsync(m => m.Team != null && m.Team.Id == teamId);
    }
}
