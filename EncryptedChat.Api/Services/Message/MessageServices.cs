using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

// True E2E: this service NEVER decrypts. It validates membership, the
// KeyGeneration invariant on writes, then persists / returns the
// encrypted envelope verbatim. All AES-GCM + ECDSA work lives on the
// client.
public class MessageService(EncryptedChatContext context) : IMessageService
{
    private readonly EncryptedChatContext _context = context;

    private const int MaxPageSize = 100;

    public async Task<IReadOnlyList<MessageDTOPublic>?> GetAllByTeamAsync(string userId, Guid teamId, int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        bool teamExists = await _context.Teams
            .AsNoTracking()
            .AnyAsync(t => t.Id == teamId);
        if (!teamExists)
            return null;

        bool isMember = await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
        if (!isMember)
            return [];

        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.Team != null && m.Team.Id == teamId)
            .OrderByDescending(m => m.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Select(m => new MessageDTOPublic
            {
                Id = m.Id,
                EncryptedText = m.EncryptedText,
                Iv = m.Iv,
                Signature = m.Signature,
                KeyGeneration = m.KeyGeneration,
                TeamId = teamId,
                Date = m.Date,
                Sender = m.Sender == null ? null : new MessageSenderDTO
                {
                    Id = m.Sender.Id,
                    Name = m.Sender.Name,
                    Handle = m.Sender.Handle,
                    NameColor = m.Sender.NameColor ?? "#FFFFFF",
                    ProfileImageUrl = m.Sender.ProfileImageUrl
                },
                Attachments = m.Attachments
                    .Select(a => new AttachmentDTOPublic
                    {
                        Id = a.Id,
                        MessageId = a.MessageId,
                        EncryptedFileName = a.EncryptedFileName,
                        FileNameIv = a.FileNameIv,
                        MimeType = a.MimeType,
                        Size = a.Size,
                        FileIv = a.FileIv,
                        Signature = a.Signature,
                        KeyGeneration = a.KeyGeneration,
                        CreatedAt = a.CreatedAt
                    }).ToList()
            })
            .ToListAsync();
    }

    public async Task<MessageDTOPublic?> GetByIdAsync(Guid id, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        Message? message = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (message?.Team == null)
            return null;

        bool isMember = await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == message.Team.Id && m.UserId == userId);
        if (!isMember)
            return null;

        return MapMessage(message);
    }

    public async Task<MessageDTOPublic?> CreateAsync(MessageDTO message, string senderId)
    {
        if (string.IsNullOrWhiteSpace(senderId))
            return null;

        if (message == null)
            return null;

        if (string.IsNullOrEmpty(message.EncryptedText)
            || string.IsNullOrEmpty(message.Iv)
            || string.IsNullOrEmpty(message.Signature))
            return null;

        User? sender = await _context.Users.FindAsync(senderId);
        if (sender == null)
            return null;

        Team? team = await _context.Teams
            .FirstOrDefaultAsync(t => t.Id == message.Team);
        if (team == null)
            return null;

        bool isMember = await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == team.Id && m.UserId == sender.Id);
        if (!isMember)
            return null;

        // E2E invariant: new messages must be encrypted under the team's
        // CURRENT generation. If the client is stale (still encrypting
        // under an old generation after a rotation), refuse the write —
        // accepting it would let an evicted member's old key decrypt
        // brand-new traffic.
        if (message.KeyGeneration != team.KeyGeneration)
            return null;

        Message newMessage = new()
        {
            Id = Guid.NewGuid(),
            EncryptedText = message.EncryptedText,
            Iv = message.Iv,
            Signature = message.Signature,
            KeyGeneration = message.KeyGeneration,
            Sender = sender,
            Team = team,
            Date = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(newMessage);
        await _context.SaveChangesAsync();

        return MapMessage(newMessage);
    }

    public async Task<MessageDTOPublic?> UpdateAsync(Guid id, MessageDTO message, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        if (message == null)
            return null;

        if (string.IsNullOrEmpty(message.EncryptedText)
            || string.IsNullOrEmpty(message.Iv)
            || string.IsNullOrEmpty(message.Signature))
            return null;

        Message? messageToUpdate = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToUpdate == null)
            return null;

        if (messageToUpdate.Sender?.Id != actorId)
            return null;

        if (messageToUpdate.Team == null)
            return null;

        // Edits must re-encrypt under the current generation. Same
        // rationale as CreateAsync.
        if (message.KeyGeneration != messageToUpdate.Team.KeyGeneration)
            return null;

        messageToUpdate.EncryptedText = message.EncryptedText;
        messageToUpdate.Iv = message.Iv;
        messageToUpdate.Signature = message.Signature;
        messageToUpdate.KeyGeneration = message.KeyGeneration;

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

        return MapMessage(messageToUpdate);
    }

    public async Task<MessageDTOPublic?> DeleteAsync(Guid id, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return null;

        Message? messageToDelete = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToDelete?.Team == null)
            return null;

        bool isOwner = messageToDelete.Sender?.Id == actorId;
        bool isAdmin = await _context.Members
            .AnyAsync(m => m.TeamId == messageToDelete.Team.Id && m.UserId == actorId && m.Role == Member.AdminRole);

        if (!isOwner && !isAdmin)
            return null;

        MessageDTOPublic dto = MapMessage(messageToDelete);

        _context.Messages.Remove(messageToDelete);
        await _context.SaveChangesAsync();

        return dto;
    }

    public Task<int> CountByTeamAsync(Guid teamId)
    {
        return _context.Messages.CountAsync(m => m.Team != null && m.Team.Id == teamId);
    }

    private bool MessageExists(Guid id)
    {
        return _context.Messages.Any(e => e.Id == id);
    }

    private static MessageDTOPublic MapMessage(Message message)
    {
        List<AttachmentDTOPublic> attachments = [];
        if (message.Attachments != null)
        {
            foreach (Attachment attachment in message.Attachments)
            {
                attachments.Add(new AttachmentDTOPublic
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
                });
            }
        }

        return new MessageDTOPublic
        {
            Id = message.Id,
            EncryptedText = message.EncryptedText,
            Iv = message.Iv,
            Signature = message.Signature,
            KeyGeneration = message.KeyGeneration,
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
            Attachments = attachments
        };
    }
}
