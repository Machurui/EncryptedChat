using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace EncryptedChat.Tests;

public class GifCacheDecoratorTests
{
    private static (GifCacheDecorator decorator, Mock<IGifService> innerMock) Create()
    {
        var inner = new Mock<IGifService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var decorator = new GifCacheDecorator(inner.Object, cache);
        return (decorator, inner);
    }

    [Fact]
    public async Task TrendingAsync_HitsInnerOnceForSameParams()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.TrendingAsync(20, 0, false, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<GifResultDTO> { new("a", "b", 100, 100) });

        await decorator.TrendingAsync(20, 0, false, CancellationToken.None);
        await decorator.TrendingAsync(20, 0, false, CancellationToken.None);

        inner.Verify(s => s.TrendingAsync(20, 0, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrendingAsync_DifferentOffsetHitsInner()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.TrendingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<GifResultDTO>());

        await decorator.TrendingAsync(20, 0, false, CancellationToken.None);
        await decorator.TrendingAsync(20, 20, false, CancellationToken.None);

        inner.Verify(s => s.TrendingAsync(20, 0, false, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(s => s.TrendingAsync(20, 20, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrendingAsync_StickersAndGifsUseSeparateCacheKeys()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.TrendingAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<GifResultDTO>());

        await decorator.TrendingAsync(20, 0, false, CancellationToken.None);
        await decorator.TrendingAsync(20, 0, true, CancellationToken.None);

        inner.Verify(s => s.TrendingAsync(20, 0, false, It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(s => s.TrendingAsync(20, 0, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CategoriesAsync_HitsInnerOnce()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.CategoriesAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<GifCategoryDTO> { new("Reactions", "url") });

        await decorator.CategoriesAsync(CancellationToken.None);
        await decorator.CategoriesAsync(CancellationToken.None);

        inner.Verify(s => s.CategoriesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_AlwaysHitsInner()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.SearchAsync("cat", 20, 0, false, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<GifResultDTO>());

        await decorator.SearchAsync("cat", 20, 0, false, CancellationToken.None);
        await decorator.SearchAsync("cat", 20, 0, false, CancellationToken.None);

        inner.Verify(s => s.SearchAsync("cat", 20, 0, false, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RandomAsync_AlwaysHitsInner_NeverCached()
    {
        var (decorator, inner) = Create();
        inner.Setup(s => s.RandomAsync(null, false, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new GifResultDTO("a", "b", 100, 100));

        await decorator.RandomAsync(null, false, CancellationToken.None);
        await decorator.RandomAsync(null, false, CancellationToken.None);

        inner.Verify(s => s.RandomAsync(null, false, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
