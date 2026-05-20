namespace EncryptedChat.Services;

public record RateLimitResult(bool Allowed, int RetryAfterMs);

public interface IRateLimitService
{
    RateLimitResult CheckAndRecord(string userId);
    void CleanupStaleEntries(TimeSpan olderThan);
}
