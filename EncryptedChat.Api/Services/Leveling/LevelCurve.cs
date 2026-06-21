namespace EncryptedChat.Services;

// Pure leveling math. The Level is DERIVED from total XP (single source of truth).
// Cumulative XP required to REACH level n = 5 * n * (n + 1):
//   Lv.1=10, Lv.2=30, Lv.3=60, Lv.5=150, Lv.10=550.
public static class LevelCurve
{
    // XP granted per qualifying message (subject to the cooldown below).
    public const int XpPerMessage = 5;

    // Anti-farm: XP is granted at most once per this window per user.
    public static readonly TimeSpan XpCooldown = TimeSpan.FromSeconds(60);

    // Cumulative XP required to reach level n (n <= 0 => 0).
    public static int XpForLevel(int n) => n <= 0 ? 0 : 5 * n * (n + 1);

    // Highest level whose threshold is <= xp (xp <= 0 => 0).
    public static int LevelForXp(int xp)
    {
        if (xp <= 0) return 0;
        int n = 0;
        while (XpForLevel(n + 1) <= xp) n++;
        return n;
    }
}
