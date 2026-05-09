using System.Net.Http.Headers;
using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class AttachmentClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public class AttachmentDTOPublic
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool SignatureVerified { get; set; }
    }

    public class Result<T>
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public T? Value { get; init; }

        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public async Task<Result<AttachmentDTOPublic>> UploadAsync(
        Guid messageId,
        Stream fileStream,
        string fileName,
        string contentType)
    {
        using MultipartFormDataContent form = new();

        StreamContent fileContent = new(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(messageId.ToString()), "messageId");

        HttpResponseMessage res = await _http.PostAsync("api/attachment", form);
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<AttachmentDTOPublic>.Fail($"Upload failed ({res.StatusCode})");

        AttachmentDTOPublic? attachment = JsonSerializer.Deserialize<AttachmentDTOPublic>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (attachment == null)
            return Result<AttachmentDTOPublic>.Fail("Invalid response");

        return Result<AttachmentDTOPublic>.Ok(attachment);
    }

    public async Task<Result<AttachmentDTOPublic>> GetMetadataAsync(Guid attachmentId)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/attachment/{attachmentId}");
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<AttachmentDTOPublic>.Fail($"Failed to get attachment ({res.StatusCode})");

        AttachmentDTOPublic? attachment = JsonSerializer.Deserialize<AttachmentDTOPublic>(
            body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (attachment == null)
            return Result<AttachmentDTOPublic>.Fail("Invalid response");

        return Result<AttachmentDTOPublic>.Ok(attachment);
    }

    public string GetDownloadUrl(Guid attachmentId)
    {
        return $"api/attachment/{attachmentId}/download";
    }

    public async Task<Result> DeleteAsync(Guid attachmentId)
    {
        HttpResponseMessage res = await _http.DeleteAsync($"api/attachment/{attachmentId}");

        if (!res.IsSuccessStatusCode)
            return Result.Fail($"Delete failed ({res.StatusCode})");

        return Result.Ok();
    }
}
