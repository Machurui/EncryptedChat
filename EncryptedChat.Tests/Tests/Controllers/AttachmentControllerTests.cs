using System.Security.Claims;
using System.Text;
using EncryptedChat.Controllers;
using EncryptedChat.Models;
using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace EncryptedChat.Tests;

public class AttachmentControllerTests
{
    private readonly Mock<IAttachmentService> _mockAttachmentService;
    private readonly Mock<IRealtimeService> _mockRealtimeService;
    private readonly string _userId = Guid.NewGuid().ToString();
    private readonly Guid _messageId = Guid.NewGuid();
    private readonly Guid _attachmentId = Guid.NewGuid();

    public AttachmentControllerTests()
    {
        _mockAttachmentService = new Mock<IAttachmentService>();
        _mockRealtimeService = new Mock<IRealtimeService>();
    }

    private AttachmentController CreateController(string? userId = null)
    {
        var controller = new AttachmentController(_mockAttachmentService.Object, _mockRealtimeService.Object);
        var claims = new List<Claim>();

        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    private static IFormFile CreateMockFile(string fileName, string content, string contentType = "text/plain")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(bytes.Length);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken _) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(target);
            });
        return file.Object;
    }

    private Task<IActionResult> InvokeUpload(AttachmentController controller, IFormFile file, Guid messageId)
        => controller.Upload(new AttachmentUploadRequest
        {
            File = file,
            MessageId = messageId,
            EncryptedFileName = "encfile",
            FileNameIv = "fniv",
            FileIv = "fiv",
            Signature = "sig",
            MimeType = "text/plain",
            KeyGeneration = 1
        });

    #region Upload

    [Fact]
    public async Task Upload_ReturnsCreatedAtAction_WhenSuccessful()
    {
        var file = CreateMockFile("test.txt", "Hello world");
        var attachment = new AttachmentDTOPublic
        {
            Id = _attachmentId,
            MessageId = _messageId,
            EncryptedFileName = "encfile",
            FileNameIv = "fniv",
            MimeType = "text/plain",
            Size = 11,
            FileIv = "fiv",
            Signature = "sig",
            KeyGeneration = 1
        };

        _mockAttachmentService
            .Setup(s => s.CreateAsync(_messageId, It.IsAny<AttachmentUploadDTO>(), _userId))
            .ReturnsAsync((attachment, null, false));

        var controller = CreateController(_userId);
        var result = await InvokeUpload(controller, file, _messageId);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AttachmentController.GetMetadata));
        createdResult.Value.Should().BeEquivalentTo(attachment);
    }

    [Fact]
    public async Task Upload_ReturnsUnauthorized_WhenNoUserId()
    {
        var file = CreateMockFile("test.txt", "content");

        var controller = CreateController(userId: null);
        var result = await InvokeUpload(controller, file, _messageId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenNoFile()
    {
        var controller = CreateController(_userId);
        var result = await InvokeUpload(controller, null!, _messageId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Upload_ReturnsForbid_WhenNotTeamMember()
    {
        var file = CreateMockFile("test.txt", "content");

        _mockAttachmentService
            .Setup(s => s.CreateAsync(_messageId, It.IsAny<AttachmentUploadDTO>(), _userId))
            .ReturnsAsync(((AttachmentDTOPublic?)null, "Accès non autorisé", true));

        var controller = CreateController(_userId);
        var result = await InvokeUpload(controller, file, _messageId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileTypeNotAllowed()
    {
        var file = CreateMockFile("malware.exe", "bad content");

        _mockAttachmentService
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<AttachmentUploadDTO>(), _userId))
            .ReturnsAsync(((AttachmentDTOPublic?)null, "Extension '.exe' non autorisée", false));

        var controller = CreateController(_userId);
        var result = await InvokeUpload(controller, file, _messageId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { Message = "Extension '.exe' non autorisée" });
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenMessageNotFound()
    {
        var file = CreateMockFile("test.txt", "content");
        var nonExistentMessageId = Guid.NewGuid();

        _mockAttachmentService
            .Setup(s => s.CreateAsync(nonExistentMessageId, It.IsAny<AttachmentUploadDTO>(), _userId))
            .ReturnsAsync(((AttachmentDTOPublic?)null, "Message introuvable", false));

        var controller = CreateController(_userId);
        var result = await InvokeUpload(controller, file, nonExistentMessageId);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { Message = "Message introuvable" });
    }

    #endregion

    #region GetMetadata

    [Fact]
    public async Task GetMetadata_ReturnsOk_WhenFound()
    {
        var attachment = new AttachmentDTOPublic
        {
            Id = _attachmentId,
            MessageId = _messageId,
            EncryptedFileName = "encfile",
            FileNameIv = "fniv",
            MimeType = "text/plain",
            Size = 100,
            FileIv = "fiv",
            Signature = "sig",
            KeyGeneration = 1
        };

        _mockAttachmentService.Setup(s => s.GetByIdAsync(_attachmentId, _userId)).ReturnsAsync(attachment);

        var controller = CreateController(_userId);
        var result = await controller.GetMetadata(_attachmentId);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(attachment);
    }

    [Fact]
    public async Task GetMetadata_ReturnsNotFound_WhenNotExists()
    {
        var nonExistentId = Guid.NewGuid();
        _mockAttachmentService.Setup(s => s.GetByIdAsync(nonExistentId, _userId)).ReturnsAsync((AttachmentDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMetadata(nonExistentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMetadata_ReturnsNotFound_WhenNotTeamMember()
    {
        _mockAttachmentService.Setup(s => s.GetByIdAsync(_attachmentId, _userId)).ReturnsAsync((AttachmentDTOPublic?)null);

        var controller = CreateController(_userId);
        var result = await controller.GetMetadata(_attachmentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMetadata_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.GetMetadata(_attachmentId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Download

    [Fact]
    public async Task Download_ReturnsFile_WhenAuthorized()
    {
        var content = new byte[] { 0x01, 0x02, 0x03 };
        var download = new AttachmentDownloadDTO
        {
            EncryptedContent = content,
            EncryptedFileName = "encfile",
            FileNameIv = "fniv",
            FileIv = "fiv",
            Signature = "sig",
            MimeType = "application/pdf",
            KeyGeneration = 1
        };

        _mockAttachmentService
            .Setup(s => s.DownloadAsync(_attachmentId, _userId))
            .ReturnsAsync(download);

        var controller = CreateController(_userId);
        var result = await controller.Download(_attachmentId);

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileContents.Should().BeEquivalentTo(content);
        fileResult.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task Download_ReturnsNotFound_WhenNotAuthorized()
    {
        _mockAttachmentService
            .Setup(s => s.DownloadAsync(_attachmentId, _userId))
            .ReturnsAsync((AttachmentDownloadDTO?)null);

        var controller = CreateController(_userId);
        var result = await controller.Download(_attachmentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.Download(_attachmentId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        _mockAttachmentService.Setup(s => s.DeleteAsync(_attachmentId, _userId)).ReturnsAsync(true);

        var controller = CreateController(_userId);
        var result = await controller.Delete(_attachmentId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotAuthorized()
    {
        _mockAttachmentService.Setup(s => s.DeleteAsync(_attachmentId, _userId)).ReturnsAsync(false);

        var controller = CreateController(_userId);
        var result = await controller.Delete(_attachmentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenNoUserId()
    {
        var controller = CreateController(userId: null);
        var result = await controller.Delete(_attachmentId);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
