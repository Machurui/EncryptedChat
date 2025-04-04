using Microsoft.EntityFrameworkCore;
using EncryptedChat.Models;

public class EncryptedChatContext : DbContext
{
    public EncryptedChatContext(DbContextOptions<EncryptedChatContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Team>()
        .HasMany(t => t.Admins)
        .WithMany(u => u.TeamsAsAdmin)
        .UsingEntity(j => j.ToTable("TeamAdmins"));

        modelBuilder.Entity<Team>()
            .HasMany(t => t.Members)
            .WithMany(u => u.TeamsAsMember)
            .UsingEntity(j => j.ToTable("TeamMembers"));
    }
}