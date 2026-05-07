using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class AuthControllerTests
{
    private static AuthController CreateControllerWithHttpContext(Mock<IAuthService> mockAuthService)
    {
        var controller = new AuthController(mockAuthService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Register_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();
        var registerDto = new RegisterDTO
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
            Name = "Test User"
        };

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync(IdentityResult.Success);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Register(registerDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenFailed()
    {
        var mockAuthService = new Mock<IAuthService>();
        var registerDto = new RegisterDTO
        {
            Email = "invalid",
            Password = "short",
            Name = "Bad Input"
        };

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid data" }));

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Register(registerDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();
        var loginDto = new LoginDTO
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
        };
        var loginResult = LoginResult.Success("access-token", DateTime.UtcNow.AddMinutes(15));

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync(loginResult);

        var controller = CreateControllerWithHttpContext(mockAuthService);

        var result = await controller.Login(loginDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenFailed()
    {
        var mockAuthService = new Mock<IAuthService>();
        var loginDto = new LoginDTO
        {
            Email = "test@example.com",
            Password = "BadPassword",
        };

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync(LoginResult.Fail("Invalid password"));

        var controller = CreateControllerWithHttpContext(mockAuthService);

        var result = await controller.Login(loginDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Logout_ReturnsOk()
    {
        var mockAuthService = new Mock<IAuthService>();
        var controller = CreateControllerWithHttpContext(mockAuthService);

        var result = controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenRefreshTokenIsInvalid()
    {
        var mockAuthService = new Mock<IAuthService>();
        var request = new AuthController.RefreshRequest("invalid-refresh-token");

        mockAuthService
            .Setup(s => s.RefreshAsync(request.refreshToken))
            .ReturnsAsync(LoginResult.Fail("Refresh not implemented"));

        var controller = CreateControllerWithHttpContext(mockAuthService);

        var result = await controller.Refresh(request);

        result.Should().BeOfType<UnauthorizedResult>();
    }
}
