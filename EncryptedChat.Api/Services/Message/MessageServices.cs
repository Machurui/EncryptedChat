using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class MessageService : IMessageService
{
    private readonly EncryptedChatContext _context;

    public MessageService(EncryptedChatContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<MessageDTOPublic>?> GetAllAsync()
    {
        // Return a list of messages
        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
            .ThenInclude(t => t!.Members)
                .ThenInclude(m => m.User)
        .AsNoTracking()
        .Select(team => ItemToDTO(team))
        .ToListAsync();
    }

    public async Task<IEnumerable<MessageDTOPublic?>?> GetAllByTeamAsync(Guid id)
    {
        // Return a list of messages by team id
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return null;

        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
            .ThenInclude(t => t!.Members)
                .ThenInclude(m => m.User)
        .AsNoTracking()
        .Where(m => m.Team != null && m.Team.Id == team.Id)
        .Select(message => ItemToDTO(message))
        .ToListAsync();
    }

    public async Task<MessageDTOPublic?> GetByIdAsync(int id)
    {
        // Return a message by id
        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
            .ThenInclude(t => t!.Members)
                .ThenInclude(m => m.User)
        .AsNoTracking()
        .Where(m => m.Id == id)
        .Select(message => ItemToDTO(message))
        .SingleOrDefaultAsync();
    }

    public async Task<MessageDTOPublic?> CreateAsync(MessageDTO message)
    {
        // Create a new message
        var sender = await _context.Users.FindAsync(message?.Sender);
        if (sender == null)
            return null;

        var teamId = message?.Team;
        if (teamId == null)
            return null;

        var team = await _context.Teams
        .Include(t => t.Members)
            .ThenInclude(m => m.User)
        .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null)
            return null;

        // Check if the sender is a member of the team
        if (team.Members == null)
            return null;

        bool isMember = team.Members.Any(m => m.UserId == sender.Id);
        if (!isMember)
            return null;

        if (string.IsNullOrWhiteSpace(message?.Text) || message.Text.Length == 0)
            return null;

        var newMessage = new Message
        {
            Text = message?.Text,
            Sender = sender,
            Team = team,
            Date = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(newMessage);
        await _context.SaveChangesAsync();

        return ItemToDTO(newMessage);
    }

    public async Task<MessageDTOPublic?> UpdateAsync(int id, MessageDTO message)
    {
        // Update a message
        var messageToUpdate = await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.Team)
                .ThenInclude(t => t!.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToUpdate == null)
            return null;

        var sender = await _context.Users.FindAsync(message?.Sender);
        if (sender == null)
            return null;

        if (message?.Team is null)
            return null;

        var team = await _context.Teams
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == message.Team.Value);

        if (team == null)
            return null;

        messageToUpdate.Text = message?.Text;

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

        return ItemToDTO(messageToUpdate);
    }

    public async Task<MessageDTOPublic?> DeleteAsync(int id)
    {
        // Delete a user
        var messageToDelete = await _context.Messages.FindAsync(id);
        if (messageToDelete == null)
            return null;

        _context.Messages.Remove(messageToDelete);
        await _context.SaveChangesAsync();

        return ItemToDTO(messageToDelete);
    }

    private bool MessageExists(int id)
    {
        return _context.Messages.Any(e => e.Id == id);
    }

    private static MessageDTOPublic ItemToDTO(Message message)
    {
        static UserDTOPublic MapUser(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Level = user.Level
        };

        static TeamDTOPublic MapTeam(Team team) => new()
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Members = [.. (team.Members ?? Enumerable.Empty<Member>()).Select(MapMember)]
        };

        static MemberDTOPublic MapMember(Member member) => new()
        {
            User = member.User is null ? null : MapUser(member.User),
            Role = member.Role
        };

        return new MessageDTOPublic
        {
            Id = message.Id,
            Text = message.Text,
            Sender = MapUser(message?.Sender ?? new User()),
            Team = MapTeam(message?.Team ?? new Team()),
            Date = message?.Date ?? DateTime.MinValue
        };
    }
}
