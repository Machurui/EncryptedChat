using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Components;

public class LightThemeProfileStatusUiTests
{
    [Fact]
    public void ProfileStatusTextHasReadableLightThemeTypographyAndContrast()
    {
        string chat = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string statusRule = ExtractCssRule(
            chat,
            ":root[data-theme=\"light\"] .profile-status-text");

        statusRule.Should().Contain("font-size: 11px;");
        statusRule.Should().Contain("font-weight: 500;");
        statusRule.Should().Contain("color: rgba(var(--text-rgb), 0.78);");
    }

    private static string ExtractCssRule(string source, string selector)
    {
        int selectorStart = source.IndexOf(selector, StringComparison.Ordinal);
        selectorStart.Should().BeGreaterThanOrEqualTo(0);

        int blockStart = source.IndexOf('{', selectorStart);
        int blockEnd = source.IndexOf('}', blockStart);
        blockStart.Should().BeGreaterThan(selectorStart);
        blockEnd.Should().BeGreaterThan(blockStart);
        return source[blockStart..blockEnd];
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        foreach (string candidateRoot in CandidateRoots())
        {
            string path = Path.Combine([candidateRoot, .. pathParts]);
            if (File.Exists(path)) return path;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(pathParts)} from test context.");
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
