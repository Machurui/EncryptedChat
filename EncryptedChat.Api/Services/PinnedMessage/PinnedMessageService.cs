using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

// True E2E: server never decrypts. Pinned messages carry the same
// encrypted envelope as regular messages; clients decrypt locally.
public class PinnedMessageService(
    EncryptedChatContext context,
    ITeamService teamService) : IPinnedMessageService
{
    private readonly EncryptedChatContext _context = context;
    private readonly ITeamService _teamService = teamService;

    public async Task<List<PinnedMessageDTO>> GetPinnedMessagesAsync(Guid teamId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return [];

        List<PinnedMessage> pins = await _context.PinnedMessages
            .Where(p => p.TeamId == teamId)
            .Include(p => p.Message)
                .ThenInclude(m => m.Sender)
            .Include(p => p.Message)
                .ThenInclude(m => m.Attachments)
            .Include(p => p.PinnedBy)
            .OrderByDescending(p => p.PinnedAt)
            .Take(50)
            .AsNoTracking()
            .ToListAsync();

        return pins.Select(MapToDTO).ToList();
    }

    public async Task<PinnedMessageDTO?> PinMessageAsync(Guid teamId, Guid messageId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return null;

        Team? team = await _context.Teams.FindAsync(teamId);
        if (team == null) return null;

        bool messageExists = await _context.Messages
            .AnyAsync(m => m.Id == messageId && m.Team != null && m.Team.Id == teamId);
        if (!messageExists) return null;

        bool alreadyPinned = await _context.PinnedMessages
            .AnyAsync(p => p.TeamId == teamId && p.MessageId == messageId);
        if (alreadyPinned) return null;

        PinnedMessage pin = new()
        {
            TeamId = teamId,
            MessageId = messageId,
            PinnedById = userId,
            PinnedAt = DateTime.UtcNow
        };

        _context.PinnedMessages.Add(pin);
        await _context.SaveChangesAsync();

        PinnedMessage? created = await _context.PinnedMessages
            .Where(p => p.Id == pin.Id)
            .Include(p => p.Message)
                .ThenInclude(m => m.Sender)
            .Include(p => p.Message)
                .ThenInclude(m => m.Attachments)
            .Include(p => p.PinnedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return created == null ? null : MapToDTO(created);
    }

    public async Task<bool> UnpinMessageAsync(Guid teamId, Guid messageId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return false;

        PinnedMessage? pin = await _context.PinnedMessages
            .FirstOrDefaultAsync(p => p.TeamId == teamId && p.MessageId == messageId);

        if (pin == null) return false;

        _context.PinnedMessages.Remove(pin);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPinnedCountAsync(Guid teamId)
    {
        return await _context.PinnedMessages.CountAsync(p => p.TeamId == teamId);
    }

    private static MessageDTOPublic MapMessage(PinnedMessage pin)
    {
        Message message = pin.Message;

        List<AttachmentDTOPublic> attachments = [];
        foreach (Attachment attachment in message.Attachments ?? [])
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
            TeamId = pin.TeamId,
            Date = message.Date,
            Attachments = attachments
        };
    }

    private static PinnedMessageDTO MapToDTO(PinnedMessage pin)
    {
        return new PinnedMessageDTO(
            pin.Id,
            pin.MessageId,
            MapMessage(pin),
            pin.PinnedById,
            pin.PinnedBy?.Name ?? "Unknown",
            pin.PinnedAt
        );
    }
}
