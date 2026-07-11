using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Components;

public class ThemeBackgroundUiTests
{
    [Fact]
    public void LightThemeWallpaperOverlayDoesNotUseWhiteVeilOrBlur()
    {
        string css = File.ReadAllText(FindRepoFile(
            "EncryptedChat.Client", "wwwroot", "css", "parts", "background.css"));

        const string selector = ":root[data-theme=\"light\"] .app-background-static::after";
        int selectorStart = css.IndexOf(selector, StringComparison.Ordinal);
        selectorStart.Should().BeGreaterThanOrEqualTo(0);

        int blockStart = css.IndexOf('{', selectorStart);
        int blockEnd = css.IndexOf('}', blockStart);
        blockStart.Should().BeGreaterThan(selectorStart);
        blockEnd.Should().BeGreaterThan(blockStart);

        string lightOverlay = css[blockStart..blockEnd];
        lightOverlay.Should().Contain("rgba(0, 0, 0, 0.20)");
        lightOverlay.Should().NotContain("rgba(255, 255, 255");
        lightOverlay.Should().NotContain("filter:");
        lightOverlay.Should().NotContain("backdrop-filter:");
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
