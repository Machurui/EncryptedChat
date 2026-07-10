namespace EncryptedChat.Tests;

using System.Text.RegularExpressions;
using FluentAssertions;

public class ClientConsoleTelemetryTests
{
    [Fact]
    public void IndexLoadsSentryConsoleBridgeBeforeApplicationScripts()
    {
        string index = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "wwwroot", "index.html"));

        int bridgeIndex = index.IndexOf("js/sentry-console.js", StringComparison.Ordinal);
        int appInteropIndex = index.IndexOf("js/app-interop.js", StringComparison.Ordinal);
        int blazorIndex = index.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal);

        bridgeIndex.Should().BeGreaterThan(0);
        bridgeIndex.Should().BeLessThan(appInteropIndex);
        bridgeIndex.Should().BeLessThan(blazorIndex);
    }

    [Fact]
    public void ApplicationWebAssetsDoNotWriteDirectlyToBrowserConsole()
    {
        string webRoot = FindRepoDirectory("EncryptedChat.Client", "wwwroot");
        Regex consoleCallPattern = new(@"\bconsole\.(?:log|debug|info|warn|error|trace|group|groupEnd|table)\s*\(");

        IEnumerable<string> directConsoleCalls = Directory.EnumerateFiles(
                webRoot,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".js" or ".ts" or ".html")
            .Where(path => !string.Equals(Path.GetFileName(path), "sentry-console.js", StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".min.js", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                string relativePath = Path.GetRelativePath(FindRepoDirectory(), path);
                return File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(entry => consoleCallPattern.IsMatch(entry.Line))
                    .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}");
            });

        directConsoleCalls.Should().BeEmpty();
    }

    [Fact]
    public void ApplicationCSharpDoesNotWriteDirectlyToBrowserConsole()
    {
        string clientRoot = FindRepoDirectory("EncryptedChat.Client");
        Regex consoleCallPattern = new(@"\b(?:System\.)?Console\.(?:Write|WriteLine|Error|Out)\b");

        IEnumerable<string> directConsoleCalls = Directory.EnumerateFiles(
                clientRoot,
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".razor")
            .Where(path => !ContainsDirectory(path, "bin") && !ContainsDirectory(path, "obj"))
            .SelectMany(path =>
            {
                string relativePath = Path.GetRelativePath(FindRepoDirectory(), path);
                return File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(entry => consoleCallPattern.IsMatch(entry.Line))
                    .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}");
            });

        directConsoleCalls.Should().BeEmpty();
    }

    [Fact]
    public void ConsoleBridgeCapturesConsoleMethodsWithoutForwardingToBrowserConsole()
    {
        string bridge = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "wwwroot", "js", "sentry-console.js"));

        foreach (string method in new[] { "log", "debug", "info", "warn", "error", "trace" })
        {
            bridge.Should().Contain($"'{method}'");
        }

        bridge.Should().Contain("DotNet.invokeMethodAsync");
        bridge.Should().Contain("CaptureConsoleMessage");
        bridge.Should().NotContain("originalConsole[method].apply");
    }

    [Fact]
    public void ClientConsoleCaptureExposesJsInvokableSentryEndpoint()
    {
        string capture = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "Observability", "ClientConsoleCapture.cs"));

        capture.Should().Contain("[JSInvokable(\"CaptureConsoleMessage\")]");
        capture.Should().Contain("SentrySdk.CaptureMessage");
        capture.Should().Contain("SentryLevel.Warning");
        capture.Should().Contain("SentryLevel.Error");
    }

    [Fact]
    public void ClientTelemetryRoutesExceptionsMessagesAndBreadcrumbsToSentry()
    {
        string telemetry = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "Observability", "ClientTelemetry.cs"));

        telemetry.Should().Contain("SentrySdk.CaptureException");
        telemetry.Should().Contain("SentrySdk.CaptureMessage");
        telemetry.Should().Contain("SentrySdk.AddBreadcrumb");
        telemetry.Should().Contain("client.operation");
    }

    [Fact]
    public void ProdComposePassesSentryDsnToWebService()
    {
        string compose = File.ReadAllText(FindRepoFile("docker-compose.prod.yml"));
        string webService = ExtractService(compose, "web");

        webService.Should().Contain("environment:");
        webService.Should().Contain("SENTRY_DSN:");
        webService.Should().Contain("SENTRY_DSN");
    }

    [Fact]
    public void ClientRuntimeConfigEntrypointWritesSentryDsnIntoAppsettings()
    {
        string dockerfile = File.ReadAllText(FindRepoFile("EncryptedChat.Client", "Dockerfile"));
        string entrypoint = File.ReadAllText(
            FindRepoFile("EncryptedChat.Client", "docker-entrypoint.d", "40-runtime-config.sh"));

        dockerfile.Should().Contain("COPY EncryptedChat.Client/docker-entrypoint.d/40-runtime-config.sh");
        entrypoint.Should().Contain("SENTRY_DSN");
        entrypoint.Should().Contain("/usr/share/nginx/html/appsettings.json");
        entrypoint.Should().Contain("\"Dsn\"");
    }

    private static string ExtractService(string compose, string serviceName)
    {
        string marker = $"  {serviceName}:";
        int start = compose.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        Match nextService = Regex.Match(compose[(start + marker.Length)..], @"\n  [a-zA-Z0-9_-]+:");
        return nextService.Success
            ? compose[start..(start + marker.Length + nextService.Index)]
            : compose[start..];
    }

    private static bool ContainsDirectory(string path, string directoryName) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, directoryName, StringComparison.Ordinal));

    private static string FindRepoDirectory(params string[] pathParts)
    {
        foreach (string candidateRoot in CandidateRoots())
        {
            string path = Path.Combine([candidateRoot, .. pathParts]);
            if (Directory.Exists(path)) return path;
        }

        throw new DirectoryNotFoundException($"Could not locate {Path.Combine(pathParts)} from test context.");
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
