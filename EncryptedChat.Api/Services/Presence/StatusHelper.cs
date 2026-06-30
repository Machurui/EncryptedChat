namespace EncryptedChat.Services;

public static class StatusHelper
{
    public static string EffectiveStatus(string? dbStatus, bool isConnected)
    {
        if (!isConnected)
            return "offline";

        if (string.IsNullOrEmpty(dbStatus))
            return "online";

        if (dbStatus == "invisible")
            return "offline";

        return dbStatus;
    }
}
