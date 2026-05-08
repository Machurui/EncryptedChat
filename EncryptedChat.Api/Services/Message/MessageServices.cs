using System.Security.Cryptography;
using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class MessageService(EncryptedChatContext context, ICryptoService crypto) : IMessageService
{
    private readonly EncryptedChatContext _context = context;
    private readonly ICryptoService _crypto = crypto;

    public async Task<IEnumerable<MessageDTOPublic>?> GetAllAsync()
    {
        List<Message> messages = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(m => m.User)
            .AsNoTracking()
            .ToListAsync();

        return messages.Select(m => DecryptAndMapMessage(m));
    }

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
            .AsNoTracking()
            .Where(m => m.Team != null && m.Team.Id == team.Id)
            .OrderByDescending(m => m.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.Select(m => DecryptAndMapMessage(m, team.Secret)).ToList();
    }

    public async Task<MessageDTOPublic?> GetByIdAsync(int id)
    {
        Message? message = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(m => m.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (message == null)
            return null;

        return DecryptAndMapMessage(message);
    }

    public async Task<MessageDTOPublic?> CreateAsync(MessageDTO message)
    {
        User? sender = await _context.Users.FindAsync(message?.Sender);
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

        if (string.IsNullOrWhiteSpace(message?.Text))
            return null;

        (string encryptedText, string iv) = _crypto.Encrypt(message.Text, team.Secret);
        string signature = _crypto.Sign(message.Text, sender.Secret);

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

    public async Task<MessageDTOPublic?> UpdateAsync(int id, MessageDTO message)
    {
        Message? messageToUpdate = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToUpdate == null)
            return null;

        User? sender = await _context.Users.FindAsync(message?.Sender);
        if (sender == null)
            return null;

        if (message?.Team is null)
            return null;

        Team? team = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == message.Team.Value);

        if (team == null)
            return null;

        if (string.IsNullOrWhiteSpace(message?.Text))
            return null;

        (string encryptedText, string iv) = _crypto.Encrypt(message.Text, team.Secret);
        string signature = _crypto.Sign(message.Text, sender.Secret);

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

    public async Task<MessageDTOPublic?> DeleteAsync(int id)
    {
        Message? messageToDelete = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToDelete == null)
            return null;

        MessageDTOPublic dto = DecryptAndMapMessage(messageToDelete);

        _context.Messages.Remove(messageToDelete);
        await _context.SaveChangesAsync();

        return dto;
    }

    private bool MessageExists(int id)
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

    private static MessageDTOPublic ItemToDTO(Message message, string text, bool signatureVerified)
    {
        return new MessageDTOPublic
        {
            Id = message.Id,
            Text = text,
            Sender = new MessageSenderDTO
            {
                Id = message.Sender?.Id ?? string.Empty,
                Name = message.Sender?.Name ?? string.Empty
            },
            TeamId = message.Team?.Id ?? Guid.Empty,
            Date = message.Date,
            SignatureVerified = signatureVerified
        };
    }
}
