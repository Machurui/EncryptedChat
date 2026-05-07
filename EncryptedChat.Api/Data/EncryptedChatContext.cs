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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
         .HasIndex(u => u.Email)
         .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Name)
            .IsUnique();

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
