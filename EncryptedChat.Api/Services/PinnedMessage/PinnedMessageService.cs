using System.Security.Cryptography;
using System.Text;
using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class PinnedMessageService(
    EncryptedChatContext context,
    ITeamService teamService,
    ICryptoService crypto) : IPinnedMessageService
{
    private readonly EncryptedChatContext _context = context;
    private readonly ITeamService _teamService = teamService;
    private readonly ICryptoService _crypto = crypto;

    public async Task<List<PinnedMessageDTO>> GetPinnedMessagesAsync(Guid teamId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return [];

        var team = await _context.Teams.FindAsync(teamId);
        if (team == null) return [];

        var pins = await _context.PinnedMessages
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

        // TEMP-Task3: return pins.Select(p => MapToDTO(p, team.Secret)).ToList();
        return pins.Select(p => MapToDTO(p, string.Empty)).ToList();
    }

    public async Task<PinnedMessageDTO?> PinMessageAsync(Guid teamId, Guid messageId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return null;

        var team = await _context.Teams.FindAsync(teamId);
        if (team == null) return null;

        bool messageExists = await _context.Messages
            .AnyAsync(m => m.Id == messageId && m.Team != null && m.Team.Id == teamId);
        if (!messageExists) return null;

        bool alreadyPinned = await _context.PinnedMessages
            .AnyAsync(p => p.TeamId == teamId && p.MessageId == messageId);
        if (alreadyPinned) return null;

        var pin = new PinnedMessage
        {
            TeamId = teamId,
            MessageId = messageId,
            PinnedById = userId,
            PinnedAt = DateTime.UtcNow
        };

        _context.PinnedMessages.Add(pin);
        await _context.SaveChangesAsync();

        var created = await _context.PinnedMessages
            .Where(p => p.Id == pin.Id)
            .Include(p => p.Message)
                .ThenInclude(m => m.Sender)
            .Include(p => p.Message)
                .ThenInclude(m => m.Attachments)
            .Include(p => p.PinnedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // TEMP-Task3: return created == null ? null : MapToDTO(created, team.Secret);
        return created == null ? null : MapToDTO(created, string.Empty);
    }

    public async Task<bool> UnpinMessageAsync(Guid teamId, Guid messageId, string userId)
    {
        bool isMember = await _teamService.IsMemberAsync(userId, teamId);
        if (!isMember) return false;

        var pin = await _context.PinnedMessages
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

    private MessageDTOPublic DecryptMessage(PinnedMessage pin, string teamSecret)
    {
        Message message = pin.Message;

        string plaintext;
        bool signatureVerified = false;

        try
        {
            // TEMP-Task3: plaintext = _crypto.Decrypt(message.EncryptedText, message.Iv, teamSecret);
            // TEMP-Task3: string senderSecret = message.Sender?.Secret ?? string.Empty;
            // TEMP-Task3: signatureVerified = _crypto.Verify(plaintext, message.Signature, senderSecret);
            plaintext = message.EncryptedText;
            signatureVerified = false;
        }
        catch (CryptographicException)
        {
            plaintext = "[Decryption failed]";
        }
        catch (FormatException)
        {
            plaintext = "[Invalid message format]";
        }

        List<AttachmentDTOPublic> attachments = [];
        foreach (var attachment in message.Attachments ?? [])
        {
            string fileName;
            try
            {
                // TEMP-Task3: byte[] encryptedFileName = Convert.FromBase64String(attachment.EncryptedFileName);
                // TEMP-Task3: byte[] fileNameBytes = _crypto.DecryptBytes(encryptedFileName, attachment.FileNameIv, teamSecret);
                // TEMP-Task3: fileName = Encoding.UTF8.GetString(fileNameBytes);
                fileName = attachment.EncryptedFileName;
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
                SignatureVerified = false
            });
        }

        return new MessageDTOPublic
        {
            Id = message.Id,
            Text = plaintext,
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
            SignatureVerified = signatureVerified,
            Attachments = attachments
        };
    }

    private PinnedMessageDTO MapToDTO(PinnedMessage pin, string teamSecret)
    {
        return new PinnedMessageDTO(
            pin.Id,
            pin.MessageId,
            DecryptMessage(pin, teamSecret),
            pin.PinnedById,
            pin.PinnedBy?.Name ?? "Unknown",
            pin.PinnedAt
        );
    }
}
