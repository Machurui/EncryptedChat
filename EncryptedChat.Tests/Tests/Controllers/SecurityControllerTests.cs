using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace EncryptedChat.Tests;

public class SecurityControllerTests
{
    private const string TestUserId = "user-123";

    private static SecurityController CreateController(
        Mock<IAuthService> mockAuth,
        Mock<IRecoveryService> mockRecovery,
        string? userId = TestUserId)
    {
        Mock<ISessionService> mockSessions = new();
        SecurityController controller = new(mockAuth.Object, mockSessions.Object, mockRecovery.Object);

        List<Claim> claims = [];
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        ClaimsIdentity identity = new(claims, "TestAuth");
        ClaimsPrincipal principal = new(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task RegenerateRecoveryPhrase_ReturnsBadRequest_WhenPasswordWrong()
    {
        Mock<IAuthService> mockAuth = new();
        Mock<IRecoveryService> mockRecovery = new();
        mockAuth
            .Setup(s => s.VerifyPasswordAsync(TestUserId, "wrong-pass"))
            .ReturnsAsync(false);

        SecurityController controller = CreateController(mockAuth, mockRecovery);

        IActionResult result = await controller.RegenerateRecoveryPhrase(
            new RecoveryPhraseRequestDTO { Password = "wrong-pass" });

        result.Should().BeOfType<BadRequestObjectResult>();
        // The phrase must NOT be rotated when the password check fails.
        mockRecovery.Verify(r => r.GenerateRecoveryPhraseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegenerateRecoveryPhrase_ReturnsBadRequest_WhenPasswordMissing()
    {
        Mock<IAuthService> mockAuth = new();
        Mock<IRecoveryService> mockRecovery = new();
        // VerifyPasswordAsync returns false for an empty password.
        mockAuth
            .Setup(s => s.VerifyPasswordAsync(TestUserId, string.Empty))
            .ReturnsAsync(false);

        SecurityController controller = CreateController(mockAuth, mockRecovery);

        IActionResult result = await controller.RegenerateRecoveryPhrase(
            new RecoveryPhraseRequestDTO { Password = string.Empty });

        result.Should().BeOfType<BadRequestObjectResult>();
        mockRecovery.Verify(r => r.GenerateRecoveryPhraseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegenerateRecoveryPhrase_ReturnsOk_WhenPasswordCorrect()
    {
        Mock<IAuthService> mockAuth = new();
        Mock<IRecoveryService> mockRecovery = new();
        mockAuth
            .Setup(s => s.VerifyPasswordAsync(TestUserId, "right-pass"))
            .ReturnsAsync(true);
        mockRecovery
            .Setup(r => r.GenerateRecoveryPhraseAsync(TestUserId))
            .ReturnsAsync(new RecoveryPhraseDTO(new[] { "word1", "word2" }, DateTime.UtcNow));

        SecurityController controller = CreateController(mockAuth, mockRecovery);

        IActionResult result = await controller.RegenerateRecoveryPhrase(
            new RecoveryPhraseRequestDTO { Password = "right-pass" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RegenerateRecoveryPhrase_ReturnsUnauthorized_WhenNoUserId()
    {
        Mock<IAuthService> mockAuth = new();
        Mock<IRecoveryService> mockRecovery = new();

        SecurityController controller = CreateController(mockAuth, mockRecovery, userId: null);

        IActionResult result = await controller.RegenerateRecoveryPhrase(
            new RecoveryPhraseRequestDTO { Password = "whatever" });

        result.Should().BeOfType<UnauthorizedResult>();
        // No password check and no rotation when the caller is unauthenticated.
        mockAuth.Verify(s => s.VerifyPasswordAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockRecovery.Verify(r => r.GenerateRecoveryPhraseAsync(It.IsAny<string>()), Times.Never);
    }
}
