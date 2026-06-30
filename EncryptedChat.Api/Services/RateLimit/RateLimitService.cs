using System.Collections.Concurrent;

namespace EncryptedChat.Services;

public class RateLimitService : IRateLimitService
{
    private const int BurstAllowance = 10;
    private const int IdleResetMs = 10_000;
    private const int MaxCooldownMs = 30_000;

    private readonly ConcurrentDictionary<string, SpamState> _states = new();

    private class SpamState
    {
        public DateTime LastSendUtc { get; set; }
        public int ConsecutiveFastSends { get; set; }
    }

    public RateLimitResult CheckAndRecord(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new RateLimitResult(true, 0);

        DateTime now = DateTime.UtcNow;
        SpamState state = _states.GetOrAdd(userId, _ => new SpamState { LastSendUtc = DateTime.MinValue });

        lock (state)
        {
            double deltaMs = (now - state.LastSendUtc).TotalMilliseconds;

            // Idle reset: long pause clears the burst counter
            if (deltaMs > IdleResetMs)
                state.ConsecutiveFastSends = 0;

            // Cooldown is based on the count AFTER this send: if this send would
            // push us past the burst allowance, enforce the corresponding cooldown.
            int excessAfterThisSend = Math.Max(0, (state.ConsecutiveFastSends + 1) - BurstAllowance);
            int cooldownMs = excessAfterThisSend == 0
                ? 0
                : Math.Min((int)Math.Pow(2, excessAfterThisSend - 1) * 1000, MaxCooldownMs);

            if (deltaMs < cooldownMs)
                return new RateLimitResult(false, (int)(cooldownMs - deltaMs));

            state.ConsecutiveFastSends++;
            state.LastSendUtc = now;
            return new RateLimitResult(true, 0);
        }
    }

    public void CleanupStaleEntries(TimeSpan olderThan)
    {
        DateTime cutoff = DateTime.UtcNow - olderThan;
        foreach (KeyValuePair<string, SpamState> kvp in _states)
        {
            if (kvp.Value.LastSendUtc < cutoff)
                _states.TryRemove(kvp.Key, out _);
        }
    }
}
