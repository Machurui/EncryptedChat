using System.Net.Http.Json;
using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class FriendClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public record FriendDTO(string UserId, string Name, string? Handle, int Level, string NameColor, string? ProfileImageUrl, DateTime FriendsSince, string Status = "offline", string? StatusMessage = null, DateTime? LastSeenAt = null);
    public record FriendRequestDTO(Guid RequestId, string UserId, string Name, string? Handle, int Level, string NameColor, string? ProfileImageUrl, DateTime SentAt, bool IsIncoming);
    public record UserDTO(string Id, string Name, string? Handle, int Level, string NameColor, string? ProfileImageUrl);

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
        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public new static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public async Task<Result<List<FriendDTO>>> GetFriendsAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/friend");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<List<FriendDTO>>.Fail(ParseMessage(body) ?? "Failed to fetch friends.");

            var friends = JsonSerializer.Deserialize<List<FriendDTO>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return Result<List<FriendDTO>>.Ok(friends);
        }
        catch
        {
            return Result<List<FriendDTO>>.Fail("Unexpected error.");
        }
    }

    public async Task<Result<List<FriendRequestDTO>>> GetPendingRequestsAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/friend/requests");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<List<FriendRequestDTO>>.Fail(ParseMessage(body) ?? "Failed to fetch requests.");

            var requests = JsonSerializer.Deserialize<List<FriendRequestDTO>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return Result<List<FriendRequestDTO>>.Ok(requests);
        }
        catch
        {
            return Result<List<FriendRequestDTO>>.Fail("Unexpected error.");
        }
    }

    public async Task<Result<List<UserDTO>>> SearchUsersToAddAsync(string query, int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Result<List<UserDTO>>.Ok([]);

            var res = await _http.GetAsync($"api/friend/search-users?q={Uri.EscapeDataString(query)}&limit={limit}");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<List<UserDTO>>.Fail(ParseMessage(body) ?? "Search failed.");

            var users = JsonSerializer.Deserialize<List<UserDTO>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return Result<List<UserDTO>>.Ok(users);
        }
        catch
        {
            return Result<List<UserDTO>>.Fail("Unexpected error.");
        }
    }

    public async Task<Result> SendRequestAsync(string addresseeId)
    {
        try
        {
            var res = await _http.PostAsync($"api/friend/{addresseeId}", null);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to send request.");

            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Unexpected error.");
        }
    }

    public async Task<Result> AcceptRequestAsync(Guid requestId)
    {
        try
        {
            var res = await _http.PostAsync($"api/friend/requests/{requestId}/accept", null);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to accept request.");

            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Unexpected error.");
        }
    }

    public async Task<Result> RejectRequestAsync(Guid requestId)
    {
        try
        {
            var res = await _http.PostAsync($"api/friend/requests/{requestId}/reject", null);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to reject request.");

            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Unexpected error.");
        }
    }

    public async Task<Result> RemoveFriendAsync(string friendId)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/friend/{friendId}");

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                return Result.Fail(ParseMessage(body) ?? "Failed to remove friend.");
            }

            return Result.Ok();
        }
        catch
        {
            return Result.Fail("Unexpected error.");
        }
    }

    private static string? ParseMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out var msg)) return null;
            return msg.ValueKind == JsonValueKind.String ? msg.GetString() : null;
        }
        catch { return null; }
    }
}
