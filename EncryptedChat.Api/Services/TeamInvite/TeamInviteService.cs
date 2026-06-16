using System.Security.Cryptography;
using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public sealed class TeamInviteService(EncryptedChatContext db, IUserKeysService userKeys) : ITeamInviteService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);
    private const int MaxActiveInvitesPerTeam = 20;
    private readonly EncryptedChatContext _db = db;
    private readonly IUserKeysService _userKeys = userKeys;

    public async Task<TeamInviteDTO?> CreateAsync(Guid teamId, string actorUserId, CancellationToken ct)
    {
        if (!await IsAdminAsync(teamId, actorUserId, ct)) return null;

        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null || team.IsDirect) return null; // no invites for DMs

        // Cap active invites per team (bound storage / anti-spam).
        var now = DateTime.UtcNow;
        var activeCount = await _db.TeamInvites
            .CountAsync(i => i.TeamId == teamId && i.RevokedAt == null && i.ExpiresAt > now, ct);
        if (activeCount >= MaxActiveInvitesPerTeam) return null;

        var invite = new TeamInvite
        {
            TeamId = teamId,
            Token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(DefaultLifetime)
        };
        _db.TeamInvites.Add(invite);
        await _db.SaveChangesAsync(ct);
        return new TeamInviteDTO(invite.Token, invite.ExpiresAt);
    }

    public async Task<List<TeamInviteListItemDTO>?> ListAsync(Guid teamId, string actorUserId, CancellationToken ct)
    {
        if (!await IsAdminAsync(teamId, actorUserId, ct)) return null;
        var now = DateTime.UtcNow;
        return await _db.TeamInvites.AsNoTracking()
            .Where(i => i.TeamId == teamId && i.RevokedAt == null && i.ExpiresAt > now)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new TeamInviteListItemDTO(i.Id, i.Token, i.CreatedAt, i.ExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeAsync(Guid teamId, Guid inviteId, string actorUserId, CancellationToken ct)
    {
        if (!await IsAdminAsync(teamId, actorUserId, ct)) return false;
        var invite = await _db.TeamInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.TeamId == teamId, ct);
        if (invite is null) return false;
        if (invite.RevokedAt is null) { invite.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
        return true;
    }

    public async Task<InvitePreviewDTO?> PreviewAsync(string token, CancellationToken ct)
    {
        var invite = await FindValidAsync(token, ct);
        if (invite is null) return null;
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invite.TeamId, ct);
        return team is null ? null : new InvitePreviewDTO(team.Id, team.Name);
    }

    public async Task<InviteJoinResult> JoinAsync(string token, string userId, CancellationToken ct)
    {
        var invite = await FindValidAsync(token, ct);
        if (invite is null) return new InviteJoinResult(InviteJoinOutcome.Invalid, null);
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == invite.TeamId, ct);
        if (team is null) return new InviteJoinResult(InviteJoinOutcome.Invalid, null);
        if (team.IsDirect) return new InviteJoinResult(InviteJoinOutcome.Invalid, null); // DMs are not joinable via invite
        var teamDto = ToPublic(team);
        if (await _db.Members.AnyAsync(m => m.TeamId == invite.TeamId && m.UserId == userId, ct))
            return new InviteJoinResult(InviteJoinOutcome.AlreadyMember, teamDto);
        if (await _userKeys.GetPublicKeysAsync(userId) is null)
            return new InviteJoinResult(InviteJoinOutcome.NoPublicKey, null);
        _db.Members.Add(new Member
        {
            Id = Guid.NewGuid(), TeamId = invite.TeamId, UserId = userId,
            Role = Member.MemberRole, UrlToken = await GenerateUniqueUrlTokenAsync(ct)
        });
        await _db.SaveChangesAsync(ct);
        return new InviteJoinResult(InviteJoinOutcome.Ok, teamDto);
    }

    private async Task<TeamInvite?> FindValidAsync(string token, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.TeamInvites.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token && i.RevokedAt == null && i.ExpiresAt > now, ct);
    }

    private async Task<bool> IsAdminAsync(Guid teamId, string userId, CancellationToken ct)
    {
        var role = await _db.Members.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .Select(m => m.Role).FirstOrDefaultAsync(ct);
        return role == Member.AdminRole || role == Member.OwnerRole;
    }

    private async Task<string> GenerateUniqueUrlTokenAsync(CancellationToken ct)
    {
        string token;
        do { token = Guid.NewGuid().ToString("N")[..16]; }
        while (await _db.Members.AnyAsync(m => m.UrlToken == token, ct));
        return token;
    }

    // Maps a Team entity to TeamDTOPublic without navigation data.
    // Members is empty (caller must fetch full team details post-join to get
    // roster). UrlToken is per-member, not per-team — left empty here.
    private static TeamDTOPublic ToPublic(Team t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Slug = t.Slug,
        Glyph = t.Glyph,
        Color = t.Color,
        MessageLifetime = t.MessageLifetime,
        IsDirect = t.IsDirect,
        KeyGeneration = t.KeyGeneration,
        Members = [],
        UrlToken = string.Empty
    };
}
