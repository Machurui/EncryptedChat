using System.Reflection;
using EncryptedChat.Client.Pages;
using FluentAssertions;

namespace EncryptedChat.Tests.Tests.Components;

public class MessageLineBreakUiTests
{
    [Fact]
    public void MessageBubblePreservesRenderedLineBreaks()
    {
        string chat = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string bubbleRule = ExtractBetween(
            chat,
            "    .message-bubble {",
            "    .message-bubble.own {");

        bubbleRule.Should().Contain("white-space: pre-wrap;");
    }

    [Fact]
    public void BothSendPathsUseNormalizedMessageText()
    {
        string chat = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Pages", "Chat.razor"));
        string sendMessage = ExtractBetween(
            chat,
            "    private async Task SendMessage()",
            "    private async Task TriggerFileInput()");

        const string normalizedCall =
            "ChatClient.SendMessageAsync(selectedTeamId.Value, messageText, teamGen, _userId!)";

        sendMessage.Should().Contain("var messageText = TrimTrailingBlankLines(newMessageText);");
        sendMessage.Split(normalizedCall, StringSplitOptions.None).Should().HaveCount(3);
        sendMessage.Should().NotContain(
            "ChatClient.SendMessageAsync(selectedTeamId.Value, newMessageText");
        sendMessage.Should().NotContain("newMessageText?.Trim()");
    }

    [Theory]
    [InlineData("hello\n\n", "hello")]
    [InlineData("hello\r\n \r\n\t", "hello")]
    [InlineData("hello\nworld\n  ", "hello\nworld")]
    [InlineData("hello\n\nworld", "hello\n\nworld")]
    [InlineData("hello", "hello")]
    public void TrailingBlankLinesAreRemovedWithoutChangingInternalLineBreaks(
        string input,
        string expected)
    {
        MethodInfo method = typeof(Chat).GetMethod(
            "TrimTrailingBlankLines",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(Chat).FullName, "TrimTrailingBlankLines");

        string result = (string)(method.Invoke(null, [input])
            ?? throw new InvalidOperationException("TrimTrailingBlankLines returned null."));

        result.Should().Be(expected);
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
