using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class GifControllerTests
{
    private readonly Mock<IGifService> _mockGifService = new();
    private readonly string _userId = Guid.NewGuid().ToString();

    private GifController CreateController(string? userId = null)
    {
        var controller = new GifController(_mockGifService.Object);
        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    [Fact]
    public async Task Search_ReturnsOk_WithResults_WhenQueryValid()
    {
        var fakeResults = new List<GifResultDTO>
        {
            new("https://x/a.gif", "https://x/a-tiny.gif", 200, 200),
            new("https://x/b.gif", "https://x/b-tiny.gif", 200, 200),
        };
        _mockGifService
            .Setup(s => s.SearchAsync("cat", 20, 0, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeResults);

        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 20, 0, ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fakeResults);
    }

    [Fact]
    public async Task Search_Returns400_WhenQueryEmpty()
    {
        var controller = CreateController(_userId);

        var result = await controller.Search("", 20, 0, ct: CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mockGifService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Search_Returns400_WhenQueryWhitespace()
    {
        var controller = CreateController(_userId);
        var result = await controller.Search("   ", 20, 0, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_Returns400_WhenLimitTooLow()
    {
        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 0, 0, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_Returns400_WhenLimitTooHigh()
    {
        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 51, 0, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_Returns400_WhenOffsetNegative()
    {
        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 20, -1, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_TrimsQueryBeforePassingToService()
    {
        _mockGifService
            .Setup(s => s.SearchAsync("cat", 20, 0, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GifResultDTO>());

        var controller = CreateController(_userId);
        await controller.Search("  cat  ", 20, 0, ct: CancellationToken.None);

        _mockGifService.Verify(
            s => s.SearchAsync("cat", 20, 0, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Search_PassesStickersTrue_WhenTypeStickers()
    {
        _mockGifService
            .Setup(s => s.SearchAsync("cat", 20, 0, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GifResultDTO>());

        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 20, 0, "stickers", CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        _mockGifService.Verify(s => s.SearchAsync("cat", 20, 0, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_Returns400_WhenTypeInvalid()
    {
        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 20, 0, "memes", CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Trending_ReturnsOk_WithResults()
    {
        var fakeResults = new List<GifResultDTO>
        {
            new("https://x/t1.gif", "https://x/t1-tiny.gif", 200, 200),
        };
        _mockGifService
            .Setup(s => s.TrendingAsync(20, 0, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeResults);

        var controller = CreateController(_userId);
        var result = await controller.Trending(20, 0, ct: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fakeResults);
    }

    [Fact]
    public async Task Trending_Returns400_WhenLimitTooHigh()
    {
        var controller = CreateController(_userId);
        var result = await controller.Trending(51, 0, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Trending_Returns400_WhenOffsetNegative()
    {
        var controller = CreateController(_userId);
        var result = await controller.Trending(20, -1, ct: CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Categories_ReturnsOk_WithCategories()
    {
        var fakeCategories = new List<GifCategoryDTO>
        {
            new("Reactions", "https://x/r.gif"),
            new("Love", "https://x/l.gif"),
        };
        _mockGifService
            .Setup(s => s.CategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeCategories);

        var controller = CreateController(_userId);
        var result = await controller.Categories(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fakeCategories);
    }

    [Fact]
    public async Task Random_ReturnsOk_WithResult()
    {
        var dto = new GifResultDTO("https://x/r.gif", "https://x/r-tiny.gif", 200, 200);
        _mockGifService.Setup(s => s.RandomAsync(null, false, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var controller = CreateController(_userId);
        var result = await controller.Random(null, "gifs", CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Random_Returns404_WhenServiceReturnsNull()
    {
        _mockGifService.Setup(s => s.RandomAsync("cat", true, It.IsAny<CancellationToken>())).ReturnsAsync((GifResultDTO?)null);

        var controller = CreateController(_userId);
        var result = await controller.Random("cat", "stickers", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Random_Returns400_WhenTypeInvalid()
    {
        var controller = CreateController(_userId);
        var result = await controller.Random(null, "memes", CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Random_Returns400_WhenTagTooLong()
    {
        var controller = CreateController(_userId);
        var result = await controller.Random(new string('x', 101), "gifs", CancellationToken.None);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
