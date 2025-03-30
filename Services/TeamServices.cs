using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class TeamService
{
    private readonly EncryptedChatContext _context;

    public TeamService(EncryptedChatContext context)
    {
        _context = context;
    }

    public IEnumerable<TeamDTOPrivate> GetAll()
    {
        // Return a list of teams
        return _context.Teams
        .Include(t => t.Admins)
        .Include(t => t.Members)
        .Select(team => ItemToDTO(team))
        .ToList();
    }

    public TeamDTOPrivate? GetById(int id)
    {
        // Return a team by id
        return _context.Teams
        .Include(t => t.Admins)
        .Include(t => t.Members)
        .AsNoTracking()
        .Where(t => t.Id == id)
        .Select(team => ItemToDTO(team))
        .SingleOrDefault();
    }

    public TeamDTOPrivate? Create(TeamDTO newTeam)
    {
        // Create a new team
        if (newTeam.Admins == null || !newTeam.Admins.Any())
            return null;

        var (members, admins) = GetEmails(newTeam);

        if (admins.Count == 0 || admins is null)
            return null;

        var team = new Team
        {
            Name = newTeam.Name,
            Password = newTeam.Password,
            Members = members,
            Admins = admins
        };

        _context.Teams.Add(team);
        _context.SaveChanges();

        return ItemToDTO(team);
    }

    public TeamDTOPrivate? Update(int id, TeamDTO team)
    {
        // Update a team
        var teamToUpdate = _context.Teams.Find(id);
        if (teamToUpdate == null)
            return null;

        var (members, admins) = GetEmails(team);

        teamToUpdate.Admins = admins;
        teamToUpdate.Members = members;
        teamToUpdate.Name = team.Name;
        teamToUpdate.Password = team.Password;

        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TeamExists(id))
            {
                return null;
            }
            else
            {
                throw;
            }
        }

        return ItemToDTO(teamToUpdate);
    }

    public TeamDTOPrivate? Delete(int id)
    {
        // Delete a team
        var teamToDelete = _context.Teams.Find(id);
        if (teamToDelete == null)
            return null;

        _context.Teams.Remove(teamToDelete);
        _context.SaveChanges();

        return ItemToDTO(teamToDelete);
    }

    private (List<User> Members, List<User> Admins) GetEmails(TeamDTO team)
    {
        var emails = (team.Members ?? [])
            .Concat(team.Admins ?? [])
            .Select(u => u.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct()
            .ToArray();

        var users = _context.Users
                    .Where(u => emails.Contains(u.Email))
                    .ToList();

        var members = users.Where(u => team.Members?.Any(m => m.Email == u.Email) ?? false).ToList();
        var admins = users.Where(u => team.Admins?.Any(a => a.Email == u.Email) ?? false).ToList();

        return (members, admins);
    }

    private bool TeamExists(int id)
    {
        return _context.Teams.Any(e => e.Id == id);
    }

    private static TeamDTOPrivate ItemToDTO(Team team)
    {
        static UserDTOPrivate MapUser(User user) => new UserDTOPrivate
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Level = user.Level
        };

        return new TeamDTOPrivate
        {
            Id = team.Id,
            Name = team.Name,
            Admins = (team.Admins ?? Enumerable.Empty<User>()).Select(MapUser).ToList(),
            Members = (team.Members ?? Enumerable.Empty<User>()).Select(MapUser).ToList()
        };
    }
}