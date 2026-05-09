using System.Net.Http.Json;
using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class TeamClient
{
    private readonly HttpClient _http;

    public TeamClient(HttpClient http)
    {
        _http = http;
    }

    // DTO match API
    public record TeamDTO(ICollection<string> Admins, ICollection<string> Members, string Name);
    public record UserDTOPublic(string Id, string Name, string Email, int Level);
    public record TeamDTOPublic(Guid Id, string Name, string Slug, string Role);

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

    // ---------- New Team ----------
    public async Task<Result> AddTeamAsync(ICollection<string> admins, ICollection<string> members, string name)
    {
        var res = await _http.PostAsJsonAsync("api/team", new TeamDTO(admins, members, name));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "The function failed.");

        return Result.Ok();
    }

    // ---------- Get Team of the user ----------
    public async Task<Result<List<TeamDTOPublic>>> GetTeamsByUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Result<List<TeamDTOPublic>>.Fail("User id is required.");

            var res = await _http.GetAsync("api/user/me/teams");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var msgAuth = ParseMessage(body) ?? "You are not authorized.";
                    return Result<List<TeamDTOPublic>>.Fail(msgAuth);
                }

                var msg = ParseMessage(body) ?? "Failed to fetch teams.";
                return Result<List<TeamDTOPublic>>.Fail(msg);
            }

            var teams = JsonSerializer.Deserialize<List<TeamDTOPublic>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? [];

            return Result<List<TeamDTOPublic>>.Ok(teams);
        }
        catch (Exception)
        {
            return Result<List<TeamDTOPublic>>.Fail("Unexpected error while fetching teams.");
        }
    }

    // ---------- Get Team Details (with members) ----------
    public record MemberDTOPublic(UserDTOPublic? User, string Role);
    public record TeamDetailDTO(Guid Id, string Name, string Slug, List<MemberDTOPublic>? Members);

    public async Task<Result<TeamDetailDTO>> GetTeamDetailsAsync(Guid teamId)
    {
        try
        {
            var res = await _http.GetAsync($"api/team/{teamId}/details");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<TeamDetailDTO>.Fail(ParseMessage(body) ?? "Failed to fetch team details.");

            var team = JsonSerializer.Deserialize<TeamDetailDTO>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (team == null)
                return Result<TeamDetailDTO>.Fail("Invalid response.");

            return Result<TeamDetailDTO>.Ok(team);
        }
        catch (Exception)
        {
            return Result<TeamDetailDTO>.Fail("Unexpected error.");
        }
    }

    // ---------- Add Member ----------
    public async Task<Result> AddMemberAsync(Guid teamId, string userId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"api/team/{teamId}/members", new { UserId = userId });
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to add member.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Remove Member ----------
    public async Task<Result> RemoveMemberAsync(Guid teamId, string userId)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/team/{teamId}/members/{userId}");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to remove member.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Promote to Admin ----------
    public async Task<Result> PromoteToAdminAsync(Guid teamId, string userId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"api/team/{teamId}/admins", new { UserId = userId });
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to promote member.");

            return Result.Ok();
        }
        catch (Exception)
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
        catch { return null; }
    }
}
