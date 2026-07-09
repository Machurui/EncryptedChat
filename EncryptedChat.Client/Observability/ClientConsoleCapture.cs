using Microsoft.JSInterop;
using Sentry;

namespace EncryptedChat.Client.Observability;

public static class ClientConsoleCapture
{
    private const int MaxMessageLength = 4000;

    [JSInvokable("CaptureConsoleMessage")]
    public static Task CaptureConsoleMessage(ClientConsoleMessage? message)
    {
        if (message is null) return Task.CompletedTask;

        string sentryMessage = BuildMessage(message);
        if (string.IsNullOrWhiteSpace(sentryMessage)) return Task.CompletedTask;

        SentrySdk.CaptureMessage(sentryMessage, MapLevel(message.Level));
        return Task.CompletedTask;
    }

    public static SentryLevel MapLevel(string? level)
    {
        return NormalizeLevel(level) switch
        {
            "error" => SentryLevel.Error,
            "warn" => SentryLevel.Warning,
            "info" => SentryLevel.Info,
            "log" => SentryLevel.Info,
            "trace" => SentryLevel.Debug,
            _ => SentryLevel.Debug
        };
    }

    public static string BuildMessage(ClientConsoleMessage message)
    {
        string level = NormalizeLevel(message.Level);
        string body = message.Message ?? string.Empty;

        if (string.IsNullOrWhiteSpace(body) && message.Arguments is { Length: > 0 })
            body = string.Join(" ", message.Arguments.Where(arg => !string.IsNullOrWhiteSpace(arg)));

        if (string.IsNullOrWhiteSpace(body))
            body = "console event";

        string source = string.IsNullOrWhiteSpace(message.Source) ? $"console.{level}" : message.Source!;
        string result = $"browser {source}: {body}";

        if (!string.IsNullOrWhiteSpace(message.Stack))
            result = $"{result}{Environment.NewLine}{message.Stack}";

        return result.Length <= MaxMessageLength
            ? result
            : string.Concat(result.AsSpan(0, MaxMessageLength), "...");
    }

    private static string NormalizeLevel(string? level) =>
        string.IsNullOrWhiteSpace(level)
            ? "log"
            : level.Trim().ToLowerInvariant();
}

public sealed class ClientConsoleMessage
{
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string[]? Arguments { get; set; }
    public string? Stack { get; set; }
    public string? Url { get; set; }
    public string? UserAgent { get; set; }
    public string? Source { get; set; }
    public string? Timestamp { get; set; }
}
