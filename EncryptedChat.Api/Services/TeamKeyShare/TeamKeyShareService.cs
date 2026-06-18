using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class TeamKeyShareService(EncryptedChatContext context) : ITeamKeyShareService
{
    private readonly EncryptedChatContext _context = context;

    public async Task<List<TeamKeyShareDTO>> GetMineForTeamAsync(string userId, Guid teamId)
    {
        bool isMember = await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
        if (!isMember) return new List<TeamKeyShareDTO>();

        return await _context.TeamKeyShares
            .AsNoTracking()
            .Where(k => k.TeamId == teamId && k.MemberId == userId)
            .OrderBy(k => k.Generation)
            .Select(k => new TeamKeyShareDTO(k.TeamId, k.Generation, k.WrappedKey, k.CreatedAt))
            .ToListAsync();
    }

    public async Task<KeyShareInsertResult> InsertKeyShareForMemberAsync(
        string adminUserId, Guid teamId, string memberId, string wrappedKey)
    {
        Member? adminMembership = await _context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == adminUserId);
        if (adminMembership == null || !Member.IsAdminOrAbove(adminMembership.Role))
            return KeyShareInsertResult.Forbidden;

        Team? team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null) return KeyShareInsertResult.NotFound;

        bool memberExists = await _context.Members
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == memberId);
        if (!memberExists) return KeyShareInsertResult.NotFound;

        bool already = await _context.TeamKeyShares
            .AnyAsync(k => k.TeamId == teamId && k.MemberId == memberId && k.Generation == team.KeyGeneration);
        if (already) return KeyShareInsertResult.AlreadyExists;

        _context.TeamKeyShares.Add(new TeamKeyShare
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            MemberId = memberId,
            Generation = team.KeyGeneration,
            WrappedKey = wrappedKey,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return KeyShareInsertResult.Ok;
    }

    public async Task<RemoveAndRotateResult> RemoveMemberAndRotateAsync(
        string adminUserId, Guid teamId, string removedMemberId,
        IReadOnlyList<KeyShareEntryDTO> newKeyShares)
    {
        Member? adminMembership = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == adminUserId);
        if (adminMembership == null || !Member.IsAdminOrAbove(adminMembership.Role))
            return RemoveAndRotateResult.Forbidden;

        Member? removedMembership = await _context.Members
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == removedMemberId);
        if (removedMembership == null) return RemoveAndRotateResult.NotFound;

        // The Owner cannot be removed by anyone — ownership must be transferred first.
        if (removedMembership.Role == Member.OwnerRole)
            return RemoveAndRotateResult.CannotRemoveOwner;

        // Can't remove the last admin
        if (removedMembership.Role == "Admin")
        {
            int adminCount = await _context.Members.CountAsync(m => m.TeamId == teamId && m.Role == "Admin");
            if (adminCount <= 1) return RemoveAndRotateResult.CannotRemoveLastAdmin;
        }

        List<string> remainingMemberIds = await _context.Members
            .AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId != removedMemberId)
            .Select(m => m.UserId)
            .ToListAsync();

        // Validate that newKeyShares covers exactly the remaining members — no missing, no extras
        HashSet<string> expectedSet = new(remainingMemberIds);
        HashSet<string> providedSet = newKeyShares.Select(k => k.MemberId).ToHashSet();
        if (!expectedSet.SetEquals(providedSet))
            return RemoveAndRotateResult.KeyShareCoverageMismatch;

        Team team = await _context.Teams.FirstAsync(t => t.Id == teamId);
        int newGeneration = team.KeyGeneration + 1;
        DateTime now = DateTime.UtcNow;

        // EF Core in-memory provider does not support transactions; the SQL Server
        // provider does. Try/catch the begin so the code still works in tests.
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException)
        {
            // In-memory provider — proceed without an explicit transaction.
        }

        try
        {
            _context.Members.Remove(removedMembership);

            List<TeamKeyShare> removedMemberShares = await _context.TeamKeyShares
                .Where(k => k.TeamId == teamId && k.MemberId == removedMemberId)
                .ToListAsync();
            _context.TeamKeyShares.RemoveRange(removedMemberShares);

            team.KeyGeneration = newGeneration;

            foreach (KeyShareEntryDTO entry in newKeyShares)
            {
                _context.TeamKeyShares.Add(new TeamKeyShare
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    MemberId = entry.MemberId,
                    Generation = newGeneration,
                    WrappedKey = entry.WrappedKey,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();
            if (transaction != null) await transaction.CommitAsync();
            return RemoveAndRotateResult.Ok;
        }
        catch
        {
            if (transaction != null) await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public async Task<List<string>?> GetMembersMissingKeyShareAsync(Guid teamId, string actorUserId)
    {
        var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId);
        if (team is null) return null;

        var actorRole = await _context.Members.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId == actorUserId)
            .Select(m => m.Role).FirstOrDefaultAsync();
        if (actorRole != Member.AdminRole && actorRole != Member.OwnerRole) return null;

        int gen = team.KeyGeneration;
        var memberIds = await _context.Members.AsNoTracking()
            .Where(m => m.TeamId == teamId).Select(m => m.UserId).ToListAsync();
        var withShare = await _context.TeamKeyShares.AsNoTracking()
            .Where(k => k.TeamId == teamId && k.Generation == gen).Select(k => k.MemberId).ToListAsync();
        var have = withShare.ToHashSet();
        return memberIds.Where(id => !have.Contains(id)).ToList();
    }
}
