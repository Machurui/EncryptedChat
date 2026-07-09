namespace EncryptedChat.Tests.Tests.Docker;

using FluentAssertions;

public class ClientNginxTests
{
    [Fact]
    public void NginxProxiesApiAndHubsToEncryptedChatApiService()
    {
        string nginx = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "nginx.conf"));
        string apiLocation = ExtractLocationBlock(nginx, "location /api/");

        apiLocation.Should().Contain("proxy_pass http://encryptedchat-api:8080;");
        apiLocation.Should().NotContain("proxy_pass http://encryptedchat-api:8080/;");
        nginx.Should().NotContain("proxy_pass http://api:8080;");
    }

    [Theory]
    [InlineData("docker-compose.yml")]
    [InlineData("docker-compose.prod.yml")]
    public void ComposeFilesExposeEncryptedChatApiNetworkAlias(string composeFile)
    {
        string compose = File.ReadAllText(FindRepoFile(composeFile));

        compose.Should().Contain("aliases:");
        compose.Should().Contain("- encryptedchat-api");
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        foreach (string candidateRoot in CandidateRoots())
        {
            string path = Path.Combine([candidateRoot, .. pathParts]);
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathParts)} from test context.");
    }

    private static string ExtractLocationBlock(string nginx, string location)
    {
        int start = nginx.IndexOf(location, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        int next = nginx.IndexOf("    location ", start + location.Length, StringComparison.Ordinal);
        return next < 0 ? nginx[start..] : nginx[start..next];
    }

    private static IEnumerable<string> CandidateRoots()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
            current = Directory.GetParent(current)?.FullName;
        }

        current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;
            current = Directory.GetParent(current)?.FullName;
        }
    }
}
