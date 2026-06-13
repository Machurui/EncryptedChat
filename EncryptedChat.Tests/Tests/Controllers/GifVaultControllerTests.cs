using System.Security.Claims;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class GifVaultControllerTests
{
    private readonly Mock<IGifVaultService> _service = new();
    private readonly string _userId = Guid.NewGuid().ToString();

    private GifVaultController CreateController(string? userId)
    {
        var controller = new GifVaultController(_service.Object);
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

    private static GifVaultWriteDTO ValidWrite(int expected = 0)
        => new("wrapped", "iv", "blob", expected);

    [Fact]
    public async Task Get_Returns204_WhenNoVault()
    {
        _service.Setup(s => s.GetAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync((GifVaultReadDTO?)null);

        var result = await CreateController(_userId).Get(CancellationToken.None);

        result.Result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Get_ReturnsOk_WithVault()
    {
        var dto = new GifVaultReadDTO("w", "iv", "blob", 3);
        _service.Setup(s => s.GetAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var result = await CreateController(_userId).Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().Be(dto);
    }

    [Fact]
    public async Task Get_Returns401_WhenNoUserClaim()
    {
        var result = await CreateController(null).Get(CancellationToken.None);
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Put_ReturnsOk_WithRevision_OnSuccess()
    {
        _service.Setup(s => s.UpsertAsync(_userId, It.IsAny<GifVaultWriteDTO>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GifVaultUpsertResult(GifVaultUpsertKind.Ok, 1));

        var result = await CreateController(_userId).Put(ValidWrite(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Put_Returns409_OnConflict()
    {
        _service.Setup(s => s.UpsertAsync(_userId, It.IsAny<GifVaultWriteDTO>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GifVaultUpsertResult(GifVaultUpsertKind.Conflict, 7));

        var result = await CreateController(_userId).Put(ValidWrite(0), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Put_Returns400_WhenBlobTooLarge()
    {
        var huge = new string('x', 256 * 1024 + 1);
        var result = await CreateController(_userId).Put(new GifVaultWriteDTO("w", "iv", huge, 0), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(s => s.UpsertAsync(It.IsAny<string>(), It.IsAny<GifVaultWriteDTO>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Put_Returns400_WhenFieldsEmpty()
    {
        var result = await CreateController(_userId).Put(new GifVaultWriteDTO("", "iv", "blob", 0), CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Put_Returns401_WhenNoUserClaim()
    {
        var result = await CreateController(null).Put(ValidWrite(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
