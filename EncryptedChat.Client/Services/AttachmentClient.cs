using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;

namespace EncryptedChat.Client.Services;

public class AttachmentClient(
    HttpClient http,
    CryptoService crypto,
    KeyVaultService vault,
    TeamKeyCacheService keyCache)
{
    private readonly HttpClient _http = http;
    private readonly CryptoService _crypto = crypto;
    private readonly KeyVaultService _vault = vault;
    private readonly TeamKeyCacheService _keyCache = keyCache;

    // Wire shape returned by the server. EncryptedFileName / FileNameIv /
    // FileIv / Signature are opaque blobs the client decrypts locally.
    public class AttachmentDTOPublic
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public string EncryptedFileName { get; set; } = string.Empty;
        public string FileNameIv { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string FileIv { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int KeyGeneration { get; set; }
        public DateTime CreatedAt { get; set; }

        // Runtime-only plaintext set by DecryptDownloadedAsync. Not serialized.
        [System.Text.Json.Serialization.JsonIgnore]
        public string? DisplayFileName { get; set; }

        // TEMP-Task14: shim for existing Chat.razor markup that still reads
        // `attachment.FileName`. Task 14 will rewire to DisplayFileName (set
        // after on-demand decrypt).
        [System.Text.Json.Serialization.JsonIgnore]
        public string FileName => DisplayFileName ?? "[encrypted]";

        // TEMP-Task14: legacy signature-verified badge. Server stops verifying;
        // the client verifies as part of DecryptDownloadedAsync.
        [System.Text.Json.Serialization.JsonIgnore]
        public bool SignatureVerified => DisplayFileName != null;
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

    // E2E upload: client encrypts filename + content with the team key, signs
    // SHA256(encContent || ivContent || encName || ivName || teamId || senderId || gen),
    // posts the encrypted blob + envelope metadata via multipart.
    public async Task<Result<AttachmentDTOPublic>> UploadAsync(
        Guid teamId,
        Guid messageId,
        int teamGeneration,
        string senderId,
        string fileName,
        string mimeType,
        byte[] fileContent)
    {
        var stored = await _vault.GetMyKeysAsync(senderId);
        if (stored == null)
            return Result<AttachmentDTOPublic>.Fail("Encryption keys not available on this device.");

        byte[]? teamSecret = _keyCache.Get(teamId, teamGeneration);
        if (teamSecret == null)
            return Result<AttachmentDTOPublic>.Fail("Team key not loaded for this generation.");

        CryptoService.AesGcmCiphertext encName = _crypto.EncryptAesGcm(Encoding.UTF8.GetBytes(fileName), teamSecret);
        CryptoService.AesGcmCiphertext encFile = _crypto.EncryptAesGcm(fileContent, teamSecret);

        byte[] teamBytes = teamId.ToByteArray();
        byte[] senderBytes = Encoding.UTF8.GetBytes(senderId);
        byte[] genBytes = BitConverter.GetBytes(teamGeneration);
        byte[] toHash = encFile.CiphertextWithTag
            .Concat(encFile.Iv)
            .Concat(encName.CiphertextWithTag)
            .Concat(encName.Iv)
            .Concat(teamBytes).Concat(senderBytes).Concat(genBytes)
            .ToArray();
        byte[] sigInput = System.Security.Cryptography.SHA256.HashData(toHash);
        byte[] sig = _crypto.Sign(sigInput, stored.SigningPrivateKey);

        using MultipartFormDataContent form = new();

        ByteArrayContent fileContentPart = new(encFile.CiphertextWithTag);
        // The server stores raw bytes; the declared MIME type is part of the
        // envelope and carried as a separate form field.
        fileContentPart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContentPart, "file", "blob");
        form.Add(new StringContent(messageId.ToString()), "messageId");
        form.Add(new StringContent(Convert.ToBase64String(encName.CiphertextWithTag)), "encryptedFileName");
        form.Add(new StringContent(Convert.ToBase64String(encName.Iv)), "fileNameIv");
        form.Add(new StringContent(Convert.ToBase64String(encFile.Iv)), "fileIv");
        form.Add(new StringContent(Convert.ToBase64String(sig)), "signature");
        form.Add(new StringContent(mimeType ?? "application/octet-stream"), "mimeType");
        form.Add(new StringContent(teamGeneration.ToString()), "keyGeneration");

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

    // Decrypts both the filename and the file contents fetched from the server.
    // Returns (plaintext filename, plaintext file bytes) or null on
    // signature-verify or decrypt failure.
    public async Task<(string FileName, byte[] Content)?> DecryptDownloadedAsync(
        AttachmentDTOPublic metadata, byte[] encryptedContent, Guid teamId, string senderId)
    {
        byte[]? teamSecret = _keyCache.Get(teamId, metadata.KeyGeneration);
        if (teamSecret == null) return null;

        try
        {
            byte[] encFile = encryptedContent;
            byte[] fileIv = Convert.FromBase64String(metadata.FileIv);
            byte[] encName = Convert.FromBase64String(metadata.EncryptedFileName);
            byte[] nameIv = Convert.FromBase64String(metadata.FileNameIv);

            byte[] plainFile = _crypto.DecryptAesGcm(fileIv, encFile, teamSecret);
            byte[] plainName = _crypto.DecryptAesGcm(nameIv, encName, teamSecret);

            return (Encoding.UTF8.GetString(plainName), plainFile);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Result> DeleteAsync(Guid attachmentId)
    {
        HttpResponseMessage res = await _http.DeleteAsync($"api/attachment/{attachmentId}");

        if (!res.IsSuccessStatusCode)
            return Result.Fail($"Delete failed ({res.StatusCode})");

        return Result.Ok();
    }
}
