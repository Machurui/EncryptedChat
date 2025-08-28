using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class TeamService : ITeamService
{
    private readonly EncryptedChatContext _context;

    public TeamService(EncryptedChatContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TeamDTOPublic?>?> GetAllAsync()
    {
        // Return a list of teams
        return await _context.Teams
        .Include(t => t.Admins)
        .Include(t => t.Members)
        .Select(team => ItemToDTO(team))
        .ToListAsync();
    }

    public async Task<TeamDTOPublic?> GetByIdAsync(int id)
    {
        // Return a team by id
        return await _context.Teams
        .Include(t => t.Admins)
        .Include(t => t.Members)
        .AsNoTracking()
        .Where(t => t.Id == id)
        .Select(team => ItemToDTO(team))
        .SingleOrDefaultAsync();
    }

    public async Task<TeamDTOPublic?> CreateAsync(TeamDTO newTeam)
    {
        // Create a team
        if (newTeam.AdminIds == null || newTeam.AdminIds.Count == 0)
            return null;

        var admins = await _context.Users
            .Where(u => newTeam.AdminIds.Contains(u.Id))
            .ToListAsync();

        var members = newTeam.MemberIds != null && newTeam.MemberIds.Count != 0
        ? await _context.Users.Where(u => newTeam.MemberIds.Contains(u.Id)).ToListAsync()
        : [];

        if (admins == null || admins.Count == 0)
            return null;

        var team = new Team
        {
            Name = newTeam.Name,
            Password = newTeam.Password,
            Admins = admins,
            Members = members
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return ItemToDTO(team);
    }

    public async Task<TeamDTOPublic?> UpdateAsync(int id, TeamDTO team)
    {
        // Update a team
        if (team.AdminIds == null || team.AdminIds.Count == 0)
            return null;

        var teamToUpdate = await _context.Teams
            .Include(t => t.Admins)
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teamToUpdate == null)
            return null;

        var admins = await _context.Users
            .Where(u => team.AdminIds.Contains(u.Id))
            .ToListAsync();

        var members = (team.MemberIds != null && team.MemberIds.Count != 0)
            ? await _context.Users.Where(u => team.MemberIds.Contains(u.Id)).ToListAsync()
            : [];

        teamToUpdate.Admins ??= [];
        teamToUpdate.Admins.Clear();
        foreach (var admin in admins)
        {
            if (!teamToUpdate.Admins.Any(a => a.Id == admin.Id))
                teamToUpdate.Admins.Add(admin);
        }

        teamToUpdate.Members ??= [];
        teamToUpdate.Members.Clear();
        foreach (var member in members)
        {
            if (!teamToUpdate.Members.Any(m => m.Id == member.Id))
                teamToUpdate.Members.Add(member);
        }

        teamToUpdate.Name = team.Name;
        teamToUpdate.Password = team.Password;

        try
        {
            if (teamToUpdate.Admins == null || teamToUpdate.Admins.Count == 0)
                return null;

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TeamExists(id))
                return null;
            throw;
        }

        return ItemToDTO(teamToUpdate);
    }

    public async Task<TeamDTOPublic?> DeleteAsync(int id)
    {
        // Delete a team
        var teamToDelete = _context.Teams.Find(id);
        if (teamToDelete == null)
            return null;

        _context.Teams.Remove(teamToDelete);
        await _context.SaveChangesAsync();

        return ItemToDTO(teamToDelete);
    }

    public async Task<bool> IsAdminAsync(string userId, int teamId)
    {
        // Check if a user is an admin is in a team
        var team = await _context.Teams
            .Include(t => t.Admins)
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team == null)
            return false;
        
        if (team.Admins == null || team.Members == null)
            return false;

        if (team.Admins.Any(a => a.Id == userId))
            return true;
        
        return false;
    }

    private bool TeamExists(int id)
    {
        return _context.Teams.Any(e => e.Id == id);
    }

    private static TeamDTOPublic ItemToDTO(Team team)
    {
        static UserDTOPublic MapUser(User user) => new UserDTOPublic
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Level = user.Level
        };

        return new TeamDTOPublic
        {
            Id = team.Id,
            Name = team.Name,
            Admins = [.. (team.Admins ?? Enumerable.Empty<User>()).Select(MapUser)],
            Members = [.. (team.Members ?? Enumerable.Empty<User>()).Select(MapUser)]
        };
    }
}