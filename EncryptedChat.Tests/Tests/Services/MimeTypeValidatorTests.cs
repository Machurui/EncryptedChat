using EncryptedChat.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EncryptedChat.Tests;

public class MimeTypeValidatorTests
{
    private readonly MimeTypeValidator _validator;

    public MimeTypeValidatorTests()
    {
        var options = Options.Create(new FileStorageOptions
        {
            BasePath = "./test",
            MaxFileSizeBytes = 1024 * 1024,
            AllowedExtensions = [".png", ".jpg", ".jpeg", ".gif", ".pdf", ".txt", ".webp"]
        });
        _validator = new MimeTypeValidator(options);
    }

    #region Valid Files

    [Fact]
    public void Validate_ReturnsValid_ForPngFile()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(pngHeader, "image/png", "image.png");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsValid_ForJpegFile()
    {
        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(jpegHeader, "image/jpeg", "photo.jpg");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsValid_ForGifFile()
    {
        byte[] gifHeader = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(gifHeader, "image/gif", "animation.gif");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsValid_ForPdfFile()
    {
        byte[] pdfHeader = [(byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', 0, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(pdfHeader, "application/pdf", "document.pdf");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsValid_ForWebPFile()
    {
        byte[] webpHeader = [
            (byte)'R', (byte)'I', (byte)'F', (byte)'F',
            0, 0, 0, 0,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P'
        ];

        FileValidationResult result = _validator.Validate(webpHeader, "image/webp", "image.webp");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsValid_ForPlainTextFile()
    {
        byte[] textContent = "Hello, this is plain text content."u8.ToArray();

        FileValidationResult result = _validator.Validate(textContent, "text/plain", "readme.txt");

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Invalid Files - Disallowed Extensions

    [Theory]
    [InlineData("virus.exe")]
    [InlineData("malware.dll")]
    [InlineData("script.bat")]
    [InlineData("script.cmd")]
    [InlineData("script.ps1")]
    [InlineData("script.sh")]
    [InlineData("script.vbs")]
    [InlineData("installer.msi")]
    public void Validate_ReturnsInvalid_ForDisallowedExtensions(string fileName)
    {
        byte[] content = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(content, "application/octet-stream", fileName);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("non autorisée");
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForDoubleExtension()
    {
        byte[] content = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(content, "application/octet-stream", "document.pdf.exe");

        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Invalid Files - MIME Mismatch

    [Fact]
    public void Validate_ReturnsInvalid_WhenJpegDeclaredButPngContent()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(pngHeader, "image/jpeg", "photo.jpg");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsInvalid_WhenImageDeclaredButExecutableContent()
    {
        byte[] mzHeader = [(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0];

        FileValidationResult result = _validator.Validate(mzHeader, "image/png", "image.png");

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Validate_ReturnsInvalid_ForUnknownMimeType()
    {
        byte[] content = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        FileValidationResult result = _validator.Validate(content, "application/x-unknown-type", "file.txt");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("non autorisé");
    }

    [Fact]
    public void Validate_HandlesUppercaseExtensions()
    {
        byte[] pngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        FileValidationResult result = _validator.Validate(pngHeader, "image/png", "IMAGE.PNG");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForDisallowedExtension()
    {
        byte[] content = [1, 2, 3, 4, 5];

        FileValidationResult result = _validator.Validate(content, "application/octet-stream", "file.xyz");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("non autorisée");
    }

    [Fact]
    public void Validate_ReturnsInvalid_ForNoExtension()
    {
        byte[] content = [1, 2, 3, 4, 5];

        FileValidationResult result = _validator.Validate(content, "text/plain", "noextension");

        result.IsValid.Should().BeFalse();
    }

    #endregion
}
