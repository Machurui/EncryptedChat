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
            new("https://x/a.gif", "https://x/a-tiny.gif"),
            new("https://x/b.gif", "https://x/b-tiny.gif"),
        };
        _mockGifService
            .Setup(s => s.SearchAsync("cat", 20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeResults);

        var controller = CreateController(_userId);
        var result = await controller.Search("cat", 20, 0, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(fakeResults);
    }

    [Fact]
    public async Task Search_Returns400_WhenQueryEmpty()
    {
        var controller = CreateController(_userId);

        var result = await controller.Search("", 20, 0, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mockGifService.Verify(
            s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Search_Returns400_WhenQueryWhitespace()
    {
        var controller = CreateController(_userId);

        var result = await controller.Search("   ", 20, 0, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_Returns400_WhenLimitTooLow()
    {
        var controller = CreateController(_userId);

        var result = await controller.Search("cat", 0, 0, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_Returns400_WhenLimitTooHigh()
    {
        var controller = CreateController(_userId);

        var result = await controller.Search("cat", 51, 0, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_TrimsQueryBeforePassingToService()
    {
        _mockGifService
            .Setup(s => s.SearchAsync("cat", 20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GifResultDTO>());

        var controller = CreateController(_userId);

        await controller.Search("  cat  ", 20, 0, CancellationToken.None);

        _mockGifService.Verify(
            s => s.SearchAsync("cat", 20, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
