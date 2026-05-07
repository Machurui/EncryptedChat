using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class UserService
{
    private readonly EncryptedChatContext _context;

    public UserService(EncryptedChatContext context)
    {
        _context = context;
    }

    public IEnumerable<UserDTOPublic> GetAll()
    {
        // Return a list of users
        return _context.Users
        .Select(user => ItemToDTO(user))
        .ToList();
    }
    public UserDTOPublic? GetById(string id)
    {
        // Return a user by id
        return _context.Users
        .AsNoTracking()
        .Where(user => user.Id == id)
        .Select(user => ItemToDTO(user))
        .SingleOrDefault();
    }

    public UserDTOPublic? Search(string? id, string? email)
    {
        if (!string.IsNullOrWhiteSpace(id))
            return _context.Users
                .Where(u => u.Id == id)
                .Select(ItemToDTO)
                .SingleOrDefault();

        if (!string.IsNullOrWhiteSpace(email))
            return _context.Users
                .Where(u => u.Email == email)
                .Select(ItemToDTO)
                .SingleOrDefault();

        return null;
    }

    public async Task<IEnumerable<MessageDTOPublic?>?> GetUserMessages(string id)
    {
        var sender = await _context.Users.FindAsync(id);
        if (sender == null)
            return null;

        return await _context.Messages
        .Include(m => m.Sender)
        .Include(m => m.Team)
            .ThenInclude(t => t!.Members)
                .ThenInclude(m => m.User)
        .AsNoTracking()
        .Where(m => m.Sender != null && m.Sender.Id == sender.Id)
        .Select(message => ItemToDTO(message))
        .ToListAsync();
    }

    public async Task<IReadOnlyList<TeamDTOPublic>> GetUserTeamsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required.", nameof(userId));

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId);

        if (!userExists)
            return Array.Empty<TeamDTOPublic>();

        var teams = await _context.Teams
            .AsNoTracking()
            .Include(t => t.Members)
                .ThenInclude(m => m.User)
            .Where(t => t.Members.Any(m => m.UserId == userId))
            .Select(team => ItemToDTO(team))
            .ToListAsync();

        return teams;
    }


    public UserDTOPublic? Update(string id, UserDTO user)
    {
        // Update a user
        var existingUser = _context.Users.Find(id);
        if (existingUser == null)
            return null;

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;

        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(id))
            {
                return null;
            }
            else
            {
                throw;
            }
        }

        return ItemToDTO(existingUser);
    }

    public User? Delete(string id)
    {
        // Delete a user
        var userToDelete = _context.Users.Find(id);
        if (userToDelete == null)
            return null;


        _context.Users.Remove(userToDelete);
        _context.SaveChanges();

        return userToDelete;
    }

    private bool UserExists(string id)
    {
        return _context.Users.Any(e => e.Id == id);
    }

    private static UserDTOPublic ItemToDTO(User user) =>
       new UserDTOPublic
       {
           Id = user.Id,
           Name = user.Name,
           Email = user.Email,
           Level = user.Level
       };

    private static MessageDTOPublic ItemToDTO(Message message)
    {
        static UserDTOPublic MapUser(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Level = user.Level
        };

        static TeamDTOPublic MapTeam(Team team) => new()
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Members = [.. (team.Members ?? Enumerable.Empty<Member>()).Select(MapMember)],
            CreatedAt = team.CreatedAt,
            ModifiedAt = team.ModifiedAt
        };

        static MemberDTOPublic MapMember(Member member) => new()
        {
            Id = member.Id,
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

    private static TeamDTOPublic ItemToDTO(Team team)
    {
        static UserDTOPublic MapUser(User user) => new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Level = user.Level
        };

        return new TeamDTOPublic
        {
            Id = team.Id,
            Name = team.Name,
            Slug = team.Slug,
            Members = [.. (team.Members ?? Enumerable.Empty<Member>()).Select(MapMember)],
            CreatedAt = team.CreatedAt,
            ModifiedAt = team.ModifiedAt
        };

        static MemberDTOPublic MapMember(Member member) => new()
        {
            Id = member.Id,
            User = member.User is null ? null : MapUser(member.User),
            Role = member.Role
        };
    }
}
