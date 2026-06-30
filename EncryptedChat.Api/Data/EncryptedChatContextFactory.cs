using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EncryptedChat.Data;

public class EncryptedChatContextFactory : IDesignTimeDbContextFactory<EncryptedChatContext>
{
    public EncryptedChatContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<EncryptedChatContext> optionsBuilder = new();
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("EncryptedChat.Api/appsettings.json", optional: true)
            .AddJsonFile($"EncryptedChat.Api/appsettings.{environment}.json", optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets<EncryptedChatContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Default' is empty.");

        optionsBuilder.UseSqlServer(connectionString);

        return new EncryptedChatContext(optionsBuilder.Options);
    }
}
