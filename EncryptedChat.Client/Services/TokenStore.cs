namespace EncryptedChat.Client.Services;

public class TokenStore
{
    public string? AccessToken { get; private set; }
    public DateTime? ExpiresUtc { get; private set; }

    public void Set(string token, DateTime expiresUtc) { AccessToken = token; ExpiresUtc = expiresUtc; }
    public void Clear() { AccessToken = null; ExpiresUtc = null; }
    public bool IsValid => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresUtc is DateTime t && DateTime.UtcNow < t;
}
