using System.ComponentModel.DataAnnotations;
using EncryptedChat.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace EncryptedChat.Tests;

public class AttachmentUploadRequestTests
{
    private static List<ValidationResult> Validate(AttachmentUploadRequest dto)
    {
        ValidationContext ctx = new(dto);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
        return results;
    }

    private static AttachmentUploadRequest Conformant() => new()
    {
        File = new FormFile(new MemoryStream([1]), 0, 1, "file", "blob"),
        MessageId = Guid.NewGuid(),
        EncryptedFileName = "Zm4=",
        FileNameIv = "AAAAAAAAAAAAAAAA",
        FileIv = "AAAAAAAAAAAAAAAA",
        Signature = "c2ln",
        MimeType = "image/png",
        KeyGeneration = 1
    };

    [Fact]
    public void Validation_Passes_WhenConformant()
    {
        Validate(Conformant()).Should().BeEmpty();
    }

    [Fact]
    public void Validation_Fails_WhenSignatureTooLong()
    {
        AttachmentUploadRequest dto = Conformant();
        dto.Signature = new string('A', 129); // Signature column = nvarchar(128)

        Validate(dto).Should().Contain(r =>
            r.MemberNames.Contains(nameof(AttachmentUploadRequest.Signature)));
    }

    [Fact]
    public void Validation_Fails_WhenFileIvTooLong()
    {
        AttachmentUploadRequest dto = Conformant();
        dto.FileIv = new string('A', 25); // FileIv column = nvarchar(24)

        Validate(dto).Should().Contain(r =>
            r.MemberNames.Contains(nameof(AttachmentUploadRequest.FileIv)));
    }

    [Fact]
    public void Validation_Fails_WhenEncryptedFileNameTooLong()
    {
        AttachmentUploadRequest dto = Conformant();
        dto.EncryptedFileName = new string('A', 513); // column = nvarchar(512)

        Validate(dto).Should().Contain(r =>
            r.MemberNames.Contains(nameof(AttachmentUploadRequest.EncryptedFileName)));
    }

    [Fact]
    public void Validation_Fails_WhenFileNameIvTooLong()
    {
        AttachmentUploadRequest dto = Conformant();
        dto.FileNameIv = new string('A', 25); // column = nvarchar(24)

        Validate(dto).Should().Contain(r =>
            r.MemberNames.Contains(nameof(AttachmentUploadRequest.FileNameIv)));
    }

    [Fact]
    public void Validation_Fails_WhenMimeTypeTooLong()
    {
        AttachmentUploadRequest dto = Conformant();
        dto.MimeType = new string('A', 101); // column = nvarchar(100)

        Validate(dto).Should().Contain(r =>
            r.MemberNames.Contains(nameof(AttachmentUploadRequest.MimeType)));
    }
}
