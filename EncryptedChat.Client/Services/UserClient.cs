using System.Net.Http.Json;
using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class UserClient
{
    private readonly HttpClient _http;

    public UserClient(HttpClient http)
    {
        _http = http;
    }

    // DTO match API
    public record UserDTOPublic(string Id, string Name, string? Handle, string Email, int Level, string NameColor = "#FFFFFF", string? ProfileImageUrl = null, string Status = "online", string? StatusMessage = null);
    public record UserProfileDTO(string Id, string Name, string? Handle, string Email, int Level, string NameColor, string? ProfileImageUrl, string Status = "online", string? StatusMessage = null, string Theme = "dark", bool ReadReceipts = true, bool TypingIndicators = true, string NotificationPreference = "all");
    public record UserUpdateDTO(string? Name = null, string? Handle = null, string? Email = null, string? NameColor = null, string? ProfileImageUrl = null, string? Status = null, string? StatusMessage = null, string? Theme = null, bool? ReadReceipts = null, bool? TypingIndicators = null, string? NotificationPreference = null);
    public record AvatarUploadResult(string Url, UserProfileDTO Profile);

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public class Result<T> : Result
    {
        public T? Value { get; init; }

        public static Result<T> Ok(T value) => new()
        {
            Success = true,
            Value = value
        };

        public new static Result<T> Fail(string msg) => new()
        {
            Success = false,
            ErrorMessage = msg
        };
    }

    // ---------- Get Profile ----------
    public async Task<Result<UserProfileDTO>> GetProfileAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/user/me");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<UserProfileDTO>.Fail(ParseMessage(body) ?? "Failed to load profile.");

            var profile = JsonSerializer.Deserialize<UserProfileDTO>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (profile == null)
                return Result<UserProfileDTO>.Fail("Invalid response.");

            return Result<UserProfileDTO>.Ok(profile);
        }
        catch (Exception)
        {
            return Result<UserProfileDTO>.Fail("Unexpected error.");
        }
    }

    // ---------- Update Profile ----------
    public async Task<Result<UserProfileDTO>> UpdateProfileAsync(UserUpdateDTO dto)
    {
        try
        {
            var res = await _http.PatchAsJsonAsync("api/user/me", dto);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<UserProfileDTO>.Fail(ParseMessage(body) ?? "Failed to update profile.");

            var profile = JsonSerializer.Deserialize<UserProfileDTO>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (profile == null)
                return Result<UserProfileDTO>.Fail("Invalid response.");

            return Result<UserProfileDTO>.Ok(profile);
        }
        catch (Exception)
        {
            return Result<UserProfileDTO>.Fail("Unexpected error.");
        }
    }

    // ---------- Get Users ----------
    public async Task<Result<List<UserDTOPublic>>> GetUsersAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/user");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var msgAuth = ParseMessage(body) ?? "You are not authorized.";
                    return Result<List<UserDTOPublic>>.Fail(msgAuth);
                }

                var msg = ParseMessage(body) ?? "Failed to fetch users.";
                return Result<List<UserDTOPublic>>.Fail(msg);
            }

            var users = JsonSerializer.Deserialize<List<UserDTOPublic>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? [];

            return Result<List<UserDTOPublic>>.Ok(users);
        }
        catch (Exception)
        {
            return Result<List<UserDTOPublic>>.Fail("Unexpected error while fetching users.");
        }
    }

    // ---------- Update Status ----------
    public async Task<Result<UserProfileDTO>> UpdateStatusAsync(string status, string? statusMessage)
    {
        return await UpdateProfileAsync(new UserUpdateDTO(Status: status, StatusMessage: statusMessage));
    }

    // ---------- Upload Avatar ----------
    public async Task<Result<AvatarUploadResult>> UploadAvatarAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "file", fileName);

            var res = await _http.PostAsync("api/user/me/avatar", content);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<AvatarUploadResult>.Fail(ParseMessage(body) ?? "Failed to upload avatar.");

            var result = JsonSerializer.Deserialize<AvatarUploadResult>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null)
                return Result<AvatarUploadResult>.Fail("Invalid response.");

            return Result<AvatarUploadResult>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<AvatarUploadResult>.Fail($"Upload error: {ex.Message}");
        }
    }

    // ---------- Delete Avatar ----------
    public async Task<Result<UserProfileDTO>> DeleteAvatarAsync()
    {
        try
        {
            var res = await _http.DeleteAsync("api/user/me/avatar");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<UserProfileDTO>.Fail(ParseMessage(body) ?? "Failed to remove avatar.");

            var profile = JsonSerializer.Deserialize<UserProfileDTO>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (profile == null)
                return Result<UserProfileDTO>.Fail("Invalid response.");

            return Result<UserProfileDTO>.Ok(profile);
        }
        catch (Exception)
        {
            return Result<UserProfileDTO>.Fail("Unexpected error.");
        }
    }

    // ---------- Update Theme ----------
    public async Task<Result<UserProfileDTO>> UpdateThemeAsync(string theme)
    {
        return await UpdateProfileAsync(new UserUpdateDTO(Theme: theme));
    }

    // ---------- Search Users ----------
    public async Task<Result<List<UserDTOPublic>>> SearchUsersAsync(string query, int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Result<List<UserDTOPublic>>.Ok([]);

            var res = await _http.GetAsync($"api/user/search?q={Uri.EscapeDataString(query)}&limit={limit}");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<List<UserDTOPublic>>.Fail(ParseMessage(body) ?? "Search failed.");

            var users = JsonSerializer.Deserialize<List<UserDTOPublic>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return Result<List<UserDTOPublic>>.Ok(users);
        }
        catch (Exception)
        {
            return Result<List<UserDTOPublic>>.Fail("Unexpected error.");
        }
    }

    private static string? ParseMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out var msg)) return null;

            return msg.ValueKind switch
            {
                JsonValueKind.String => msg.GetString(),
                JsonValueKind.Array => string.Join("\n",
                    msg.EnumerateArray()
                       .Where(e => e.ValueKind == JsonValueKind.String)
                       .Select(e => e.GetString())
                       .Where(s => !string.IsNullOrWhiteSpace(s))),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}