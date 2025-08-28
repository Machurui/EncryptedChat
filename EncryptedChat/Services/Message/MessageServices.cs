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
        .Select(team => ItemToDTO(team))
        .ToListAsync();
    }

    public async Task<IEnumerable<MessageDTOPublic?>?> GetAllByTeamAsync(int id)
    {
        // Return a list of messages by team id
        var team = await _context.Teams.FindAsync(id);
        if (team == null)
            return null;

        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
        // .Include(m => m.Team!.Admins) To maintain readability
        // .Include(m => m.Team!.Members)
        .Where(m => m.Team != null && m.Team.Id == team.Id)
        .Select(message => ItemToDTO(message))
        .ToListAsync();
    }

<<<<<<<< HEAD:EncryptedChat.Api/Services/MessageServices.cs
    
========

>>>>>>>> origin/Auth_v1.0:EncryptedChat/Services/Message/MessageServices.cs

    public async Task<MessageDTOPublic?> GetByIdAsync(int id)
    {
        // Return a message by id
        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
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
        .Include(t => t.Admins)
        .Include(t => t.Members)
        .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null)
            return null;

        // Check if the sender is a member of the team
        if (team.Members == null || team.Admins == null)
            return null;

        bool isMember = team.Members.Any(u => u.Id == sender.Id);
        bool isAdmin = team.Admins.Any(u => u.Id == sender.Id);

        if (!isMember && !isAdmin)
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
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (messageToUpdate == null)
            return null;

        var sender = await _context.Users.FindAsync(message?.Sender);
        if (sender == null)
            return null;

        var team = await _context.Teams.FindAsync(message?.Team);
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
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Level = user.Level
        };

        static TeamDTOPublic MapTeam(Team team) => new()
        {
            Id = team.Id,
            Name = team.Name,
            Admins = [.. (team.Admins ?? Enumerable.Empty<User>()).Select(MapUser)],
            Members = [.. (team.Members ?? Enumerable.Empty<User>()).Select(MapUser)]
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