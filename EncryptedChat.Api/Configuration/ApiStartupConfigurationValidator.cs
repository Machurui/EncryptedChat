using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Data.SqlClient;

namespace EncryptedChat.Configuration;

public static class ApiStartupConfigurationValidator
{
    private const string PlaceholderPrefix = "SET_IN_";

    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<string> errors = [];

        ValidateConnectionString(configuration.GetConnectionString("Default"), errors);
        ValidateRequiredValue(configuration["Jwt:Issuer"], "Jwt:Issuer", "Jwt__Issuer", errors);
        ValidateRequiredValue(configuration["Jwt:Audience"], "Jwt:Audience", "Jwt__Audience", errors);
        ValidateJwtKey(configuration["Jwt:Key"], errors);
        ValidateEncryptionKey(configuration["Encryption:Key"], errors);
        ValidateBoolean(configuration["RunMigrationsOnStartup"], errors);

        if (errors.Count == 0)
            return;

        throw new InvalidOperationException(
            "API startup configuration is invalid. Secret values are not logged.\n- " +
            string.Join("\n- ", errors));
    }

    private static void ValidateConnectionString(string? value, ICollection<string> errors)
    {
        const string message =
            "ConnectionStrings:Default must be a valid SQL Server connection string with Server and Database " +
            "(Kubernetes environment variable: ConnectionStrings__Default).";

        if (IsMissingOrPlaceholder(value))
        {
            errors.Add(message);
            return;
        }

        try
        {
            SqlConnectionStringBuilder connectionString = new(value);
            if (string.IsNullOrWhiteSpace(connectionString.DataSource) ||
                string.IsNullOrWhiteSpace(connectionString.InitialCatalog))
            {
                errors.Add(message);
            }
        }
        catch (ArgumentException)
        {
            errors.Add(message);
        }
    }

    private static void ValidateJwtKey(string? value, ICollection<string> errors)
    {
        if (IsMissingOrPlaceholder(value) || Encoding.UTF8.GetByteCount(value) < 32)
        {
            errors.Add(
                "Jwt:Key must contain at least 32 UTF-8 bytes " +
                "(Kubernetes environment variable: Jwt__Key).");
        }
    }

    private static void ValidateEncryptionKey(string? value, ICollection<string> errors)
    {
        const string message =
            "Encryption:Key must be base64 for exactly 32 bytes " +
            "(Kubernetes environment variable: Encryption__Key).";

        if (IsMissingOrPlaceholder(value))
        {
            errors.Add(message);
            return;
        }

        try
        {
            if (Convert.FromBase64String(value).Length != 32)
                errors.Add(message);
        }
        catch (FormatException)
        {
            errors.Add(message);
        }
    }

    private static void ValidateRequiredValue(
        string? value,
        string configurationKey,
        string environmentVariable,
        ICollection<string> errors)
    {
        if (IsMissingOrPlaceholder(value))
        {
            errors.Add(
                $"{configurationKey} is required " +
                $"(Kubernetes environment variable: {environmentVariable}).");
        }
    }

    private static void ValidateBoolean(string? value, ICollection<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && !bool.TryParse(value, out _))
        {
            errors.Add(
                "RunMigrationsOnStartup must be 'true' or 'false' " +
                "(Kubernetes environment variable: RunMigrationsOnStartup).");
        }
    }

    private static bool IsMissingOrPlaceholder([NotNullWhen(false)] string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Trim().StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase);
}
