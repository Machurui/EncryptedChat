using Sentry;

namespace EncryptedChat.Client.Observability;

public static class ClientTelemetry
{
    private const string OperationTag = "client.operation";

    public static void CaptureError(Exception exception, string operation) =>
        CaptureException(exception, operation, SentryLevel.Error);

    public static void CaptureWarning(Exception exception, string operation) =>
        CaptureException(exception, operation, SentryLevel.Warning);

    public static void CaptureWarning(string message, string operation)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        SentrySdk.CaptureMessage(
            message,
            scope => scope.SetTag(OperationTag, NormalizeOperation(operation)),
            SentryLevel.Warning);
    }

    public static void AddBreadcrumb(string message, string category)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        SentrySdk.AddBreadcrumb(
            message,
            category: NormalizeOperation(category),
            level: BreadcrumbLevel.Info);
    }

    private static void CaptureException(Exception exception, string operation, SentryLevel level)
    {
        ArgumentNullException.ThrowIfNull(exception);

        SentrySdk.CaptureException(exception, scope =>
        {
            scope.Level = level;
            scope.SetTag(OperationTag, NormalizeOperation(operation));
        });
    }

    private static string NormalizeOperation(string? operation) =>
        string.IsNullOrWhiteSpace(operation) ? "unknown" : operation.Trim();
}
