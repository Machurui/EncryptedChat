namespace EncryptedChat.Client.Services;

public static class NotificationPolicy
{
    /// Decides whether an incoming message should raise a browser toast.
    /// Badge counting is independent and lives in Chat.razor.
    public static bool ShouldShowToast(
        string? preference,   // "all" | "mentions" | "none"
        bool isDm,
        bool isMentioned,
        string? status,       // "online" | "away" | "busy" | "invisible"
        bool isOwnMessage,
        bool isMuted)
    {
        if (isOwnMessage) return false;
        if (isMuted) return false;
        if (status == "busy") return false; // Do Not Disturb suppresses toasts

        return (preference ?? "all").ToLowerInvariant().Trim() switch
        {
            "none" => false,
            "mentions" => isDm || isMentioned,
            _ => true,
        };
    }
}
