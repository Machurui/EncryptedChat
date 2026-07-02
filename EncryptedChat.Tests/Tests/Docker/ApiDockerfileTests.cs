namespace EncryptedChat.Tests.Tests.Docker;

using FluentAssertions;

public class ApiDockerfileTests
{
    [Fact]
    public void RuntimeImageOwnsAvatarUploadDirectoryBeforeDroppingRoot()
    {
        string dockerfile = File.ReadAllText(FindRepoFile("EncryptedChat.Api", "Dockerfile"));
        int userAppIndex = dockerfile.IndexOf("USER app", StringComparison.Ordinal);

        userAppIndex.Should().BeGreaterThan(0, "runtime setup must happen before dropping privileges");
        string runtimeSetup = dockerfile[..userAppIndex];

        runtimeSetup.Should().Contain("/app/wwwroot/uploads/avatars");
        runtimeSetup.Should().Contain("chown -R app:app");
        runtimeSetup.Should().Contain("/app/wwwroot");
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
