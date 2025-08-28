using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using Microsoft.AspNetCore.Identity;
using EncryptedChat.Services;
using System.Security.Claims;

namespace EncryptedChat.Tests;

public class AuthControllerTests
{
    // Test for Register method

    [Fact]
    public async Task Register_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();
        var registerDto = new RegisterDTO
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
            FirstName = "Test",
            LastName = "User"
        };

        var identityResult = IdentityResult.Success;

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync(identityResult);

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
            FirstName = "Bad",
            LastName = "Input"
        };

        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Invalid data" });

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync(identityResult);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Register(registerDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsAlreadyUsed()
    {
        var mockAuthService = new Mock<IAuthService>();
        var registerDto = new RegisterDTO
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
            FirstName = "Test",
            LastName = "User"
        };

        var identityResult = IdentityResult.Failed(new IdentityError { Description = "Invalid data" });

        mockAuthService
            .Setup(s => s.RegisterAsync(It.IsAny<RegisterDTO>()))
            .ReturnsAsync(identityResult);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Register(registerDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }


    // Test for Login method

    [Fact]
    public async Task Login_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();

        var loginDto = new LoginDTO
        {
            Email = "test@example.com",
            Password = "P@ssw0rd",
        };

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Login(loginDto);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenWrongPassword()
    {
        var mockAuthService = new Mock<IAuthService>();

        var loginDto = new LoginDTO
        {
            Email = "test@example.com",
            Password = "BadPassword",
        };

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Login(loginDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenNotExisting()
    {
        var mockAuthService = new Mock<IAuthService>();

        var loginDto = new LoginDTO
        {
            Email = "bad@example.com",
            Password = "BadPassword",
        };

        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginDTO>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Login(loginDto);

        result.Should().BeOfType<BadRequestObjectResult>();
    }


    // Test for Logout method

    [Fact]
    public async Task Logout_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();

        mockAuthService
            .Setup(s => s.LogoutAsync())
            .ReturnsAsync(new SignOutResult());

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Logout();

        result.Should().BeOfType<OkObjectResult>();
    }


    // Test for Refresh method
    [Fact]
    public async Task Refresh_ReturnsOk_WhenSuccessful()
    {
        var mockAuthService = new Mock<IAuthService>();

        mockAuthService
            .Setup(s => s.RefreshAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Refresh();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenFailed()
    {
        var mockAuthService = new Mock<IAuthService>();

        mockAuthService
            .Setup(s => s.RefreshAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = new AuthController(mockAuthService.Object);

        var result = await controller.Refresh();

        result.Should().BeOfType<UnauthorizedResult>();
    }

}
