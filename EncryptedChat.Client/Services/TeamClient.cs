using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;
using static EncryptedChat.Client.Services.Crypto.KeyVaultService;
using static EncryptedChat.Client.Services.UserClient;

namespace EncryptedChat.Client.Services;

public class TeamClient(HttpClient http, CryptoService crypto, KeyVaultService vault, TeamKeyCacheService keyCache, UserClient userClient, IKeyVerificationService keyVerify)
{
    private readonly HttpClient _http = http;
    private readonly CryptoService _crypto = crypto;
    private readonly KeyVaultService _vault = vault;
    private readonly TeamKeyCacheService _keyCache = keyCache;
    private readonly UserClient _userClient = userClient;
    private readonly IKeyVerificationService _keyVerify = keyVerify;

    public record TeamDTO(ICollection<string> Admins, ICollection<string> Members, string Name, string? Glyph = null, string? Color = null, string? MessageLifetime = null, string? InitialKeyShare = null, Dictionary<string, string>? MemberKeyShares = null);
    public record TeamKeyShareResponse(Guid TeamId, int Generation, string WrappedKey, DateTime CreatedAt);
    public record UserDTOPublic(string Id, string Name, string? Handle, string Email, int Level, string NameColor = "#FFFFFF", string? ProfileImageUrl = null, string Status = "offline");
    public record TeamDTOPublic(
        Guid Id,
        string Name,
        string Slug,
        string Role,
        string Glyph = "◆",
        string Color = "oklch(0.65 0.16 165)",
        string MessageLifetime = "off",
        bool IsDirect = false,
        string? LastMessagePreview = null,
        DateTime? LastMessageTime = null,
        string? LastMessageSenderName = null,
        string UrlToken = "",
        int KeyGeneration = 1,
        int UnreadCount = 0,
        bool IsMuted = false);
    public record TeamUpdateDTO(string? Name = null, string? Glyph = null, string? Color = null, string? MessageLifetime = null);

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

    public async Task<Result<TeamDTOPublic>> AddTeamAsync(string creatorUserId, ICollection<string> admins, ICollection<string> members, string name, string? glyph = null, string? color = null, string? messageLifetime = null)
    {
        HashSet<string> allMemberIds = [.. admins];
        foreach (string m in members) allMemberIds.Add(m);
        allMemberIds.Add(creatorUserId);

        Dictionary<string, Task<UserClient.PublicKeysResponse?>> fetchTasks = allMemberIds.ToDictionary(
            id => id,
            _userClient.GetPublicKeysAsync);
        await Task.WhenAll(fetchTasks.Values);

        Dictionary<string, UserClient.PublicKeysResponse?> pubKeys = fetchTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
        List<string> missing = [.. pubKeys.Where(kv => kv.Value == null).Select(kv => kv.Key)];
        if (missing.Count > 0)
            return Result<TeamDTOPublic>.Fail(
                $"Cannot create team: encryption keys not set up for member(s) {string.Join(", ", missing)}.");

        byte[] teamSecret = await _crypto.GenerateTeamSecretAsync();

        Dictionary<string, string> memberShares = [];
        foreach ((string id, UserClient.PublicKeysResponse? keys) in pubKeys)
        {
            if (await _keyVerify.CheckAndPinAsync(id, keys!.SigningPublicKey, keys.EncryptionPublicKey)
                    == KeyPinResult.Changed)
                throw new KeyChangedException(id);
            byte[] pub = Convert.FromBase64String(keys.EncryptionPublicKey);
            byte[] wrapped = await _crypto.WrapKeyAsync(teamSecret, pub);
            memberShares[id] = Convert.ToBase64String(wrapped);
        }

        TeamDTO dto = new(admins, members, name, glyph, color, messageLifetime, null, memberShares);
        HttpResponseMessage res = await _http.PostAsJsonAsync("api/team", dto);
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "The function failed.");

        TeamDTOPublic? team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (team == null)
            return Result<TeamDTOPublic>.Fail("Invalid response.");

        _keyCache.Put(team.Id, 1, teamSecret);

        return Result<TeamDTOPublic>.Ok(team);
    }

    // ---------- E2E key-share lifecycle ----------

    public async Task LoadKeySharesIntoCacheAsync(Guid teamId, string myUserId)
    {
        StoredKeys? stored = await _vault.GetMyKeysAsync(myUserId);
        if (stored == null) 
            return;

        HttpResponseMessage res = await _http.GetAsync($"api/Team/{teamId}/key-shares");
        if (!res.IsSuccessStatusCode) 
            return;

        List<TeamKeyShareResponse>? shares = await res.Content.ReadFromJsonAsync<List<TeamKeyShareResponse>>();
        if (shares == null) 
            return;

        foreach (TeamKeyShareResponse s in shares)
        {
            byte[] wrappedBlob = Convert.FromBase64String(s.WrappedKey);
            try
            {
                byte[] teamSecret = await _crypto.UnwrapKeyAsync(wrappedBlob, stored.EncryptionPrivateKey);
                _keyCache.Put(s.TeamId, s.Generation, teamSecret);
            }
            catch
            {
                // Drop silently — corrupted or unrelated row.
            }
        }
    }

    // Admin-only: wraps the current Team.Secret for a new member's pubkey.
    public async Task<bool> AddMemberKeyShareAsync(Guid teamId, int generation, string newMemberId)
    {
        byte[]? teamSecret = _keyCache.Get(teamId, generation);
        if (teamSecret == null) 
            return false;

        PublicKeysResponse? newMemberPubKeys = await _userClient.GetPublicKeysAsync(newMemberId);
        if (newMemberPubKeys == null) 
            return false;

        if (await _keyVerify.CheckAndPinAsync(newMemberId, newMemberPubKeys.SigningPublicKey, newMemberPubKeys.EncryptionPublicKey)
                == KeyPinResult.Changed)
            throw new KeyChangedException(newMemberId);
        byte[] pub = Convert.FromBase64String(newMemberPubKeys.EncryptionPublicKey);

        byte[] wrappedBlob = await _crypto.WrapKeyAsync(teamSecret, pub);
        HttpResponseMessage res = await _http.PostAsJsonAsync(
            $"api/Team/{teamId}/members/{Uri.EscapeDataString(newMemberId)}/key-share",
            new { WrappedKey = Convert.ToBase64String(wrappedBlob) });

        return res.IsSuccessStatusCode;
    }

    public async Task ProvisionMissingKeySharesAsync(Guid teamId, int generation)
    {
        try
        {
            HttpResponseMessage res = await _http.GetAsync($"api/Team/{teamId}/members/missing-key-share");
            if (!res.IsSuccessStatusCode) 
                return; // 403 (not admin) / error → nothing to do

            List<string>? missing = await res.Content.ReadFromJsonAsync<List<string>>();
            if (missing is null) 
                return;

            foreach (string userId in missing)
            {
                try { await AddMemberKeyShareAsync(teamId, generation, userId); }
                catch (Exception ex) { Console.WriteLine($"[invite] provision {userId} failed: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[invite] provision failed: {ex.Message}"); }
    }

    // Admin-only: generate a fresh Team.Secret, wrap for every remaining member,
    // post the bundle so the server can atomically delete + rotate + reinsert.
    public async Task<bool> RemoveMemberWithRotationAsync(Guid teamId, int currentGeneration, string removedMemberId, IReadOnlyList<string> remainingMemberIds)
    {
        byte[] newTeamSecret = await _crypto.GenerateTeamSecretAsync();

        List<object> newKeyShares = [];
        foreach (string memberId in remainingMemberIds)
        {
            PublicKeysResponse? pubKeys = await _userClient.GetPublicKeysAsync(memberId);
            if (pubKeys == null) 
                return false;

            if (await _keyVerify.CheckAndPinAsync(memberId, pubKeys.SigningPublicKey, pubKeys.EncryptionPublicKey)
                    == KeyPinResult.Changed)
                throw new KeyChangedException(memberId);
            byte[] pub = Convert.FromBase64String(pubKeys.EncryptionPublicKey);
            byte[] wrappedBlob = await _crypto.WrapKeyAsync(newTeamSecret, pub);
            newKeyShares.Add(new { MemberId = memberId, WrappedKey = Convert.ToBase64String(wrappedBlob) });
        }

        HttpResponseMessage res = await _http.PostAsJsonAsync(
            $"api/Team/{teamId}/members/{Uri.EscapeDataString(removedMemberId)}/remove",
            new { NewKeyShares = newKeyShares });
        if (!res.IsSuccessStatusCode) 
            return false;

        _keyCache.Put(teamId, currentGeneration + 1, newTeamSecret);
        return true;
    }

    // ---------- Update Team ----------
    public async Task<Result<TeamDTOPublic>> UpdateTeamAsync(Guid teamId, TeamUpdateDTO dto)
    {
        try
        {
            HttpResponseMessage res = await _http.PatchAsJsonAsync($"api/team/{teamId}", dto);
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "Failed to update team.");

            TeamDTOPublic? team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (team == null)
                return Result<TeamDTOPublic>.Fail("Invalid response.");

            return Result<TeamDTOPublic>.Ok(team);
        }
        catch (Exception)
        {
            return Result<TeamDTOPublic>.Fail("Unexpected error.");
        }
    }

    // ---------- Delete Team ----------
    public async Task<Result> DeleteTeamAsync(Guid teamId)
    {
        try
        {
            HttpResponseMessage res = await _http.DeleteAsync($"api/team/{teamId}");
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to delete team.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Get Team of the user ----------
    public async Task<Result<List<TeamDTOPublic>>> GetTeamsByUserAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
                return Result<List<TeamDTOPublic>>.Fail("User id is required.");

            HttpResponseMessage res = await _http.GetAsync("api/user/me/teams");
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized || res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    string msgAuth = ParseMessage(body) ?? "You are not authorized.";
                    return Result<List<TeamDTOPublic>>.Fail(msgAuth);
                }

                string msg = ParseMessage(body) ?? "Failed to fetch teams.";
                return Result<List<TeamDTOPublic>>.Fail(msg);
            }

            List<TeamDTOPublic> teams = JsonSerializer.Deserialize<List<TeamDTOPublic>>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? [];

            return Result<List<TeamDTOPublic>>.Ok(teams);
        }
        catch (Exception)
        {
            return Result<List<TeamDTOPublic>>.Fail("Unexpected error while fetching teams.");
        }
    }

    // ---------- Mark a conversation read (server read-marker) ----------
    public async Task<bool> MarkReadAsync(Guid teamId)
    {
        try
        {
            HttpResponseMessage res = await _http.PostAsync($"api/team/{teamId}/read", null);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ---------- Mute / unmute a conversation (per-member) ----------
    public async Task<bool> SetMutedAsync(Guid teamId, bool muted)
    {
        try
        {
            HttpResponseMessage res = await _http.PutAsJsonAsync($"api/team/{teamId}/mute", new { Muted = muted });
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ---------- Get Team Details (with members) ----------
    public record MemberDTOPublic(UserDTOPublic? User, string Role);
    public record TeamDetailDTO(Guid Id, string Name, string Slug, string Glyph, string Color, string MessageLifetime, bool IsDirect, List<MemberDTOPublic>? Members);

    public async Task<Result<TeamDTOPublic>> GetTeamByTokenAsync(string token)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/team/by-token/{token}");
        if (!res.IsSuccessStatusCode)
            return Result<TeamDTOPublic>.Fail($"Team not found ({res.StatusCode}).");

        string body = await res.Content.ReadAsStringAsync();
        TeamDTOPublic? team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return team != null
            ? Result<TeamDTOPublic>.Ok(team)
            : Result<TeamDTOPublic>.Fail("Invalid response.");
    }

    public async Task<Result<TeamDetailDTO>> GetTeamDetailsAsync(Guid teamId)
    {
        try
        {
            HttpResponseMessage res = await _http.GetAsync($"api/team/{teamId}/details");
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<TeamDetailDTO>.Fail(ParseMessage(body) ?? "Failed to fetch team details.");

            TeamDetailDTO? team = JsonSerializer.Deserialize<TeamDetailDTO>(body,
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
            HttpResponseMessage res = await _http.PostAsJsonAsync($"api/team/{teamId}/members", new { UserId = userId });
            string body = await res.Content.ReadAsStringAsync();

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
            HttpResponseMessage res = await _http.DeleteAsync($"api/team/{teamId}/members/{userId}");
            string body = await res.Content.ReadAsStringAsync();

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
            HttpResponseMessage res = await _http.PostAsJsonAsync($"api/team/{teamId}/admins", new { UserId = userId });
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to promote member.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Transfer Ownership ----------
    public async Task<Result> TransferOwnershipAsync(Guid teamId, string newOwnerId)
    {
        try
        {
            HttpResponseMessage res = await _http.PostAsJsonAsync($"api/team/{teamId}/owner", new { UserId = newOwnerId });
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to transfer ownership.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Demote from Admin ----------

    public record Payload (string MyWrappedKey, string FriendWrappedKey);

    public async Task<Result> DemoteFromAdminAsync(Guid teamId, string userId)
    {
        try
        {
            HttpResponseMessage res = await _http.DeleteAsync($"api/team/{teamId}/admins/{userId}");
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to demote member.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    public async Task<Result<TeamDTOPublic>> GetOrCreateDirectMessageAsync(string myUserId, string friendId)
    {
        Console.WriteLine($"[DM] GetOrCreateDirectMessageAsync start. myUserId={myUserId}, friendId={friendId}");
        try
        {
            PublicKeysResponse? myPubKeys = await _userClient.GetPublicKeysAsync(myUserId);

            PublicKeysResponse? friendPubKeys = await _userClient.GetPublicKeysAsync(friendId);
            if (myPubKeys == null || friendPubKeys == null)
                return Result<TeamDTOPublic>.Fail("Cannot fetch public keys for DM bootstrap.");

            if (await _keyVerify.CheckAndPinAsync(friendId, friendPubKeys.SigningPublicKey, friendPubKeys.EncryptionPublicKey)
                    == KeyPinResult.Changed)
                return Result<TeamDTOPublic>.Fail("KEY_CHANGED:" + friendId);

            byte[] myPub = Convert.FromBase64String(myPubKeys.EncryptionPublicKey);
            byte[] friendPub = Convert.FromBase64String(friendPubKeys.EncryptionPublicKey);

            byte[] teamSecret = await _crypto.GenerateTeamSecretAsync();
            byte[] myWrapped = await _crypto.WrapKeyAsync(teamSecret, myPub);
            byte[] friendWrapped = await _crypto.WrapKeyAsync(teamSecret, friendPub);

            Payload payload = new
            (
                MyWrappedKey: Convert.ToBase64String(myWrapped),
                FriendWrappedKey: Convert.ToBase64String(friendWrapped)
            );

            HttpResponseMessage res = await _http.PostAsJsonAsync($"api/team/dm/{friendId}", payload);
            string body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "Failed to create direct message.");

            TeamDTOPublic? dm = JsonSerializer.Deserialize<TeamDTOPublic>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dm == null)
                return Result<TeamDTOPublic>.Fail("Invalid response.");

            _keyCache.Put(dm.Id, 1, teamSecret);

            return Result<TeamDTOPublic>.Ok(dm);
        }
        catch (Exception ex)
        {
            return Result<TeamDTOPublic>.Fail($"Unexpected error: {ex.Message}");
        }
    }

    private static string? ParseMessage(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out JsonElement msg)) return null;

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

public sealed class KeyChangedException(string userId)
    : Exception($"Safety number changed for user {userId}; secret-sharing refused.")
{
    public string UserId { get; } = userId;
}
