using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace EncryptedChat.Tests;

public class AuthControllerTests
{
    private static AuthController CreateControllerWithHttpContext(Mock<IAuthService> mockAuthService)
    {
        // Provide a default IConfiguration that mirrors production defaults
        // (Secure=true, SameSite=None) so cookie helpers work in unit tests.
        IConfiguration config = new ConfigurationBuilder().Build();
        AuthController controller = new(mockAuthService.Object, config);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenSuccessful()
    {
        Mock<IAuthService> mockAuthService = new();
        RegisterDTO registerDto = new()
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
            Handle = "testuser"
        };

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync((IdentityResult.Success, (IReadOnlyList<string>?)new[] { "abandon" }, (string?)"test-jwt-token"));

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Register(registerDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenFailed()
    {
        Mock<IAuthService> mockAuthService = new();
        RegisterDTO registerDto = new()
        {
            Email = "invalid@x.com",
            Password = "short1",
            Handle = "badinput"
        };

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync((IdentityResult.Failed(new IdentityError { Description = "Invalid data" }), (IReadOnlyList<string>?)null, (string?)null));

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Register(registerDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenSuccessful()
    {
        Mock<IAuthService> mockAuthService = new();
        LoginDTO loginDto = new()
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
        };
        LoginResult loginResult = LoginResult.Success("access-token", DateTime.UtcNow.AddMinutes(15), "refresh-token");

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(loginResult);

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Login(loginDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenFailed()
    {
        Mock<IAuthService> mockAuthService = new();
        LoginDTO loginDto = new()
        {
            Email = "test@example.com",
            Password = "BadPassword",
        };

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(LoginResult.Fail("Invalid credentials"));

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Login(loginDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        Mock<IAuthService> mockAuthService = new();
        mockAuthService
            .Setup(s => s.LogoutAsync(It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsOk_WhenRefreshTokenIsValid()
    {
        Mock<IAuthService> mockAuthService = new();
        AuthController.RefreshRequest request = new("valid-refresh-token");
        LoginResult loginResult = LoginResult.Success("new-access-token", DateTime.UtcNow.AddMinutes(15), "new-refresh-token");

        mockAuthService
            .Setup(s => s.RefreshAsync("valid-refresh-token", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(loginResult);

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Refresh(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenRefreshTokenIsInvalid()
    {
        Mock<IAuthService> mockAuthService = new();
        AuthController.RefreshRequest request = new("invalid-refresh-token");

        mockAuthService
            .Setup(s => s.RefreshAsync("invalid-refresh-token", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(LoginResult.Fail("Invalid refresh token"));

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Refresh(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenNoRefreshToken()
    {
        Mock<IAuthService> mockAuthService = new();
        AuthController.RefreshRequest? request = null;

        AuthController controller = CreateControllerWithHttpContext(mockAuthService);

        IActionResult result = await controller.Refresh(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
