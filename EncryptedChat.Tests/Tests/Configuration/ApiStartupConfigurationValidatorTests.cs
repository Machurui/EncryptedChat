using EncryptedChat.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EncryptedChat.Tests;

public class ApiStartupConfigurationValidatorTests
{
    private const string EncryptionKey = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    [Fact]
    public void Validate_AcceptsValidConfiguration()
    {
        IConfiguration configuration = BuildValidConfiguration();

        Action act = () => ApiStartupConfigurationValidator.Validate(configuration);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ReportsEveryRequiredKubernetesVariable()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Action act = () => ApiStartupConfigurationValidator.Validate(configuration);

        InvalidOperationException exception = act.Should()
            .Throw<InvalidOperationException>()
            .Which;
        exception.Message.Should().Contain("ConnectionStrings__Default");
        exception.Message.Should().Contain("Jwt__Issuer");
        exception.Message.Should().Contain("Jwt__Audience");
        exception.Message.Should().Contain("Jwt__Key");
        exception.Message.Should().Contain("Encryption__Key");
    }

    [Fact]
    public void Validate_RejectsInvalidValuesWithoutLoggingThem()
    {
        const string invalidConnectionString = "not-a-connection-string-secret";
        const string weakJwtKey = "weak-jwt-secret";
        const string invalidEncryptionKey = "invalid-encryption-secret";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = invalidConnectionString,
                ["Jwt:Issuer"] = "EncryptedChat",
                ["Jwt:Audience"] = "EncryptedChat.Client",
                ["Jwt:Key"] = weakJwtKey,
                ["Encryption:Key"] = invalidEncryptionKey,
                ["RunMigrationsOnStartup"] = "yes"
            })
            .Build();

        Action act = () => ApiStartupConfigurationValidator.Validate(configuration);

        InvalidOperationException exception = act.Should()
            .Throw<InvalidOperationException>()
            .Which;
        exception.Message.Should().NotContain(invalidConnectionString);
        exception.Message.Should().NotContain(weakJwtKey);
        exception.Message.Should().NotContain(invalidEncryptionKey);
        exception.Message.Should().Contain("RunMigrationsOnStartup");
    }

    [Fact]
    public void Validate_RejectsConnectionStringWithoutDatabase()
    {
        IConfiguration configuration = BuildValidConfiguration(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Server=sqlserver;User Id=app;Password=test-password;Encrypt=True;TrustServerCertificate=True"
        });

        Action act = () => ApiStartupConfigurationValidator.Validate(configuration);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings__Default*");
    }

    private static IConfiguration BuildValidConfiguration(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:Default"] =
                "Server=sqlserver;Database=EncryptedChat;User Id=app;Password=test-password;Encrypt=True;TrustServerCertificate=True",
            ["Jwt:Issuer"] = "EncryptedChat",
            ["Jwt:Audience"] = "EncryptedChat.Client",
            ["Jwt:Key"] = "0123456789abcdef0123456789abcdef",
            ["Encryption:Key"] = EncryptionKey,
            ["RunMigrationsOnStartup"] = "true"
        };

        if (overrides is not null)
        {
            foreach ((string key, string? value) in overrides)
                values[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
