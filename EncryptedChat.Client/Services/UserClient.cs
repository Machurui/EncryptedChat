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
    public record UserDTOPublic(string Id, string Name, string Email, int Level);

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