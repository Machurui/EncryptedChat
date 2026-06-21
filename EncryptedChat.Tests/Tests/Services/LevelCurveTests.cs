using EncryptedChat.Services;
using FluentAssertions;

namespace EncryptedChat.Tests;

public class LevelCurveTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 60)]
    [InlineData(5, 150)]
    [InlineData(10, 550)]
    public void XpForLevel_MatchesCurve(int level, int expectedXp)
    {
        LevelCurve.XpForLevel(level).Should().Be(expectedXp);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(149, 4)]
    [InlineData(150, 5)]
    public void LevelForXp_ReturnsHighestReachedLevel(int xp, int expectedLevel)
    {
        LevelCurve.LevelForXp(xp).Should().Be(expectedLevel);
    }

    [Fact]
    public void LevelForXp_IsConsistentWithXpForLevel()
    {
        for (int n = 0; n <= 20; n++)
        {
            LevelCurve.LevelForXp(LevelCurve.XpForLevel(n)).Should().Be(n);
            LevelCurve.LevelForXp(LevelCurve.XpForLevel(n) - 1).Should().Be(n == 0 ? 0 : n - 1);
        }
    }
}
