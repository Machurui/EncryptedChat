using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Components;

public class MessageStreamBorderUiTests
{
    [Fact]
    public void MessageStreamBorderRendersAboveScrollingContent()
    {
        string chat = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string shineRule = ExtractBetween(
            chat,
            "    .message-stream-panel > .glass-shine {",
            "    .message-stream-panel,");

        shineRule.Should().Contain("z-index: 4;");
    }

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);
        return source[start..end];
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
