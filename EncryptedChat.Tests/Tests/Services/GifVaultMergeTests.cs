using EncryptedChat.Client.Services;
using FluentAssertions;

namespace EncryptedChat.Tests;

public class GifVaultMergeTests
{
    private const long Now = 1_000_000_000_000;

    private static GifItem Fav(string url, long ts) => new(url, url + "-p", 10, 10, "gifs", ts);

    [Fact]
    public void Merge_UnionsFavorites_AddWins_WhenNoTombstone()
    {
        var local = new GifVaultState { Favorites = { Fav("A", 100) } };
        var remote = new GifVaultState { Favorites = { Fav("B", 200) } };

        var merged = GifVaultMerge.Merge(local, remote, Now);

        merged.Favorites.Select(f => f.Url).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public void Merge_RemovalWins_WhenTombstoneNewerThanAdd()
    {
        // Realistic epoch-ms timestamps: a recent removal that is newer than the add.
        var local = new GifVaultState { Favorites = { Fav("A", Now - 2000) } };
        var remote = new GifVaultState { Tombstones = { new GifTomb("A", Now - 1000) } };

        var merged = GifVaultMerge.Merge(local, remote, Now);

        merged.Favorites.Should().BeEmpty();
        merged.Tombstones.Should().ContainSingle(t => t.Url == "A");
    }

    [Fact]
    public void Merge_ReAddWins_WhenAddNewerThanTombstone()
    {
        var local = new GifVaultState { Tombstones = { new GifTomb("A", 200) } };
        var remote = new GifVaultState { Favorites = { Fav("A", 300) } };

        var merged = GifVaultMerge.Merge(local, remote, Now);

        merged.Favorites.Should().ContainSingle(f => f.Url == "A");
        merged.Tombstones.Should().BeEmpty(); // obsolete once the add won
    }

    [Fact]
    public void Merge_Recents_UnionByUrl_KeepsNewestTs()
    {
        var local = new GifVaultState { Recents = { Fav("A", 100) } };
        var remote = new GifVaultState { Recents = { Fav("A", 500) } };

        var merged = GifVaultMerge.Merge(local, remote, Now);

        merged.Recents.Should().ContainSingle(r => r.Url == "A").Which.Ts.Should().Be(500);
    }

    [Fact]
    public void Merge_Recents_CapsAtMaxRecents_KeepingNewest()
    {
        var local = new GifVaultState();
        for (int i = 0; i < 40; i++) local.Recents.Add(Fav($"U{i}", i));

        var merged = GifVaultMerge.Merge(local, new GifVaultState(), Now);

        merged.Recents.Should().HaveCount(GifVaultMerge.MaxRecents);
        merged.Recents.First().Ts.Should().Be(39); // newest first
        merged.Recents.Should().NotContain(r => r.Url == "U0");
    }

    [Fact]
    public void Merge_PrunesTombstones_OlderThanTtl()
    {
        var old = Now - GifVaultMerge.TombstoneTtlMs - 1;
        var local = new GifVaultState { Tombstones = { new GifTomb("Old", old), new GifTomb("Fresh", Now - 1000) } };

        var merged = GifVaultMerge.Merge(local, new GifVaultState(), Now);

        merged.Tombstones.Select(t => t.Url).Should().BeEquivalentTo(new[] { "Fresh" });
    }

    [Fact]
    public void Merge_IsIdempotent_OnRepeatedMerge()
    {
        var s = new GifVaultState { Favorites = { Fav("A", 100), Fav("B", 200) }, Recents = { Fav("C", 50) } };

        var once = GifVaultMerge.Merge(s, new GifVaultState(), Now);
        var twice = GifVaultMerge.Merge(once, new GifVaultState(), Now);

        twice.Favorites.Select(f => f.Url).Should().BeEquivalentTo(once.Favorites.Select(f => f.Url));
        twice.Recents.Select(r => r.Url).Should().BeEquivalentTo(once.Recents.Select(r => r.Url));
    }
}
