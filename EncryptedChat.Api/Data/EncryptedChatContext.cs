using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Data;
public class EncryptedChatContext(DbContextOptions<EncryptedChatContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<PinnedMessage> PinnedMessages => Set<PinnedMessage>();
    public DbSet<UserTeamPreference> UserTeamPreferences => Set<UserTeamPreference>();
    public DbSet<PasswordHistoryEntry> PasswordHistory => Set<PasswordHistoryEntry>();
    public DbSet<TeamKeyShare> TeamKeyShares => Set<TeamKeyShare>();
    public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();
    public DbSet<UserGifVault> UserGifVaults => Set<UserGifVault>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
         .HasIndex(u => u.Email)
         .IsUnique();

        modelBuilder.Entity<UserTeamPreference>()
            .HasKey(p => new { p.UserId, p.TeamId });

        modelBuilder.Entity<UserTeamPreference>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserTeamPreference>()
            .HasOne(p => p.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            // ClientCascade avoids SQL Server's multi-cascade-path error
            // (User→UserTeamPreferences already cascades). EF Core deletes
            // UserTeamPreferences when a Team is removed via the in-memory
            // change tracker rather than via DB-level cascade.
            .OnDelete(DeleteBehavior.ClientCascade);

        modelBuilder.Entity<UserGifVault>()
            .HasKey(v => v.UserId);

        modelBuilder.Entity<UserGifVault>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Team>()
            .HasMany(t => t.Members)
            .WithOne(m => m.Team)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Team>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Member>()
            .HasIndex(m => new { m.TeamId, m.UserId })
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => m.UrlToken)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attachment>()
            .HasIndex(a => a.MessageId);

        modelBuilder.Entity<Friendship>()
            .HasOne(f => f.Requester)
            .WithMany()
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Friendship>()
            .HasOne(f => f.Addressee)
            .WithMany()
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Friendship>()
            .HasIndex(f => new { f.RequesterId, f.AddresseeId })
            .IsUnique();

        modelBuilder.Entity<Session>()
            .HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Session>()
            .HasIndex(s => s.TokenHash);

        modelBuilder.Entity<Session>()
            .HasIndex(s => new { s.UserId, s.IsRevoked });

        modelBuilder.Entity<Session>()
            .HasOne(s => s.CurrentRefreshToken)
            .WithMany()
            .HasForeignKey(s => s.CurrentRefreshTokenId)
            // NoAction avoids SQL Server's multi-cascade-path error (User cascades
            // to both Sessions and RefreshTokens). Refresh tokens are never hard
            // deleted today (only RevokedAt is set); if a cleanup ever deletes
            // rows, it must null this FK first.
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Session>()
            .HasIndex(s => s.CurrentRefreshTokenId);

        modelBuilder.Entity<PinnedMessage>()
            .HasOne(p => p.Team)
            .WithMany()
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<PinnedMessage>()
            .HasOne(p => p.Message)
            .WithMany()
            .HasForeignKey(p => p.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PinnedMessage>()
            .HasOne(p => p.PinnedBy)
            .WithMany()
            .HasForeignKey(p => p.PinnedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PinnedMessage>()
            .HasIndex(p => new { p.TeamId, p.MessageId })
            .IsUnique();

        modelBuilder.Entity<PasswordHistoryEntry>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PasswordHistoryEntry>()
            .HasIndex(p => new { p.UserId, p.CreatedAt });

        modelBuilder.Entity<TeamKeyShare>()
            .HasOne(k => k.Team)
            .WithMany()
            .HasForeignKey(k => k.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TeamKeyShare>()
            .HasOne(k => k.Member)
            .WithMany()
            .HasForeignKey(k => k.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TeamKeyShare>()
            .HasIndex(k => new { k.TeamId, k.MemberId, k.Generation })
            .IsUnique();

        modelBuilder.Entity<TeamKeyShare>()
            .HasIndex(k => k.MemberId);

        modelBuilder.Entity<TeamInvite>()
            .HasIndex(i => i.Token)
            .IsUnique();

        modelBuilder.Entity<TeamInvite>()
            .HasOne(i => i.Team)
            .WithMany()
            .HasForeignKey(i => i.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "7e3f74c2-7b73-45eb-b62b-0a119d2b40f1",
                Name = "User",
                NormalizedName = "USER",
                ConcurrencyStamp = "7e3f74c2-7b73-45eb-b62b-0a119d2b40f1"
            },
            new IdentityRole
            {
                Id = "63bbbc1f-f1d8-4e12-901d-05eb0c8b79d0",
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "63bbbc1f-f1d8-4e12-901d-05eb0c8b79d0"
            },
            new IdentityRole
            {
                Id = "e7ef2982-f1ad-4568-b0cc-b8f61bbf6822",
                Name = "App",
                NormalizedName = "APP",
                ConcurrencyStamp = "e7ef2982-f1ad-4568-b0cc-b8f61bbf6822"
            });
    }
}
