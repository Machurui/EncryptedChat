using System.Net.Http.Json;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;

namespace EncryptedChat.Client.Services;

public class TeamClient
{
    private readonly HttpClient _http;
    private readonly CryptoService _crypto;
    private readonly KeyVaultService _vault;
    private readonly TeamKeyCacheService _keyCache;
    private readonly UserClient _userClient;

    public TeamClient(HttpClient http, CryptoService crypto, KeyVaultService vault, TeamKeyCacheService keyCache, UserClient userClient)
    {
        _http = http;
        _crypto = crypto;
        _vault = vault;
        _keyCache = keyCache;
        _userClient = userClient;
    }

    // DTO match API. MemberKeyShares (userId → base64 ECIES-wrapped secret)
    // must cover every entry in Admins ∪ Members including the creator —
    // server rejects creation otherwise. InitialKeyShare is kept for back-
    // compat with single-member creator-only teams.
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
        int KeyGeneration = 1);
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

    // ---------- New Team ----------
    // E2E: the client generates a random 32-byte Team.Secret, wraps it for the
    // creator's EncryptionPublicKey (ECIES-P256), and posts the wrapped blob
    // alongside team metadata. The server inserts a TeamKeyShare row at
    // generation 1. The plaintext Team.Secret is cached locally so the creator
    // can immediately encrypt their first message without an extra unwrap.
    public async Task<Result<TeamDTOPublic>> AddTeamAsync(string creatorUserId, ICollection<string> admins, ICollection<string> members, string name, string? glyph = null, string? color = null, string? messageLifetime = null)
    {
        // Aggregate every member that will be on the team — admins ∪ members
        // ∪ creator (creator is forced into the admin set server-side too).
        // The server now requires a wrapped Team.Secret for every one of
        // them, otherwise it rejects with 400 and no team is created.
        HashSet<string> allMemberIds = new(admins);
        foreach (var m in members) allMemberIds.Add(m);
        allMemberIds.Add(creatorUserId);

        // Fetch each member's encryption public key in parallel. If any
        // member has no key bootstrapped, bail before creating the team —
        // otherwise we'd ship a team with a stuck member.
        var fetchTasks = allMemberIds.ToDictionary(
            id => id,
            id => _userClient.GetPublicKeysAsync(id));
        await Task.WhenAll(fetchTasks.Values);

        var pubKeys = fetchTasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);
        var missing = pubKeys.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
        if (missing.Count > 0)
            return Result<TeamDTOPublic>.Fail(
                $"Cannot create team: encryption keys not set up for member(s) {string.Join(", ", missing)}.");

        byte[] teamSecret = await _crypto.GenerateTeamSecretAsync();

        Dictionary<string, string> memberShares = new();
        foreach (var (id, keys) in pubKeys)
        {
            byte[] pub = Convert.FromBase64String(keys!.EncryptionPublicKey);
            byte[] wrapped = await _crypto.WrapKeyAsync(teamSecret, pub);
            memberShares[id] = Convert.ToBase64String(wrapped);
        }

        // InitialKeyShare kept null — the server now picks up MemberKeyShares
        // which covers the creator too. Sending both would be redundant.
        var dto = new TeamDTO(admins, members, name, glyph, color, messageLifetime, null, memberShares);
        var res = await _http.PostAsJsonAsync("api/team", dto);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "The function failed.");

        var team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (team == null)
            return Result<TeamDTOPublic>.Fail("Invalid response.");

        // Cache for the creator so they can encrypt immediately. Other
        // members fetch + unwrap via LoadKeySharesIntoCacheAsync the first
        // time they open the team.
        _keyCache.Put(team.Id, 1, teamSecret);

        return Result<TeamDTOPublic>.Ok(team);
    }

    // ---------- E2E key-share lifecycle ----------

    // Loads the caller's TeamKeyShare rows for a team and unwraps each into the
    // cache. Idempotent — call when opening a team or on bootstrap.
    public async Task LoadKeySharesIntoCacheAsync(Guid teamId, string myUserId)
    {
        var stored = await _vault.GetMyKeysAsync(myUserId);
        if (stored == null) return;

        var res = await _http.GetAsync($"api/Team/{teamId}/key-shares");
        if (!res.IsSuccessStatusCode) return;
        var shares = await res.Content.ReadFromJsonAsync<List<TeamKeyShareResponse>>();
        if (shares == null) return;

        foreach (var s in shares)
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
        if (teamSecret == null) return false;

        var newMemberPubKeys = await _userClient.GetPublicKeysAsync(newMemberId);
        if (newMemberPubKeys == null) return false;
        byte[] pub = Convert.FromBase64String(newMemberPubKeys.EncryptionPublicKey);

        byte[] wrappedBlob = await _crypto.WrapKeyAsync(teamSecret, pub);
        var res = await _http.PostAsJsonAsync(
            $"api/Team/{teamId}/members/{Uri.EscapeDataString(newMemberId)}/key-share",
            new { WrappedKey = Convert.ToBase64String(wrappedBlob) });
        return res.IsSuccessStatusCode;
    }

    // Admin-only: generate a fresh Team.Secret, wrap for every remaining member,
    // post the bundle so the server can atomically delete + rotate + reinsert.
    public async Task<bool> RemoveMemberWithRotationAsync(Guid teamId, int currentGeneration, string removedMemberId, IReadOnlyList<string> remainingMemberIds)
    {
        byte[] newTeamSecret = await _crypto.GenerateTeamSecretAsync();

        var newKeyShares = new List<object>();
        foreach (var memberId in remainingMemberIds)
        {
            var pubKeys = await _userClient.GetPublicKeysAsync(memberId);
            if (pubKeys == null) return false;
            byte[] pub = Convert.FromBase64String(pubKeys.EncryptionPublicKey);
            byte[] wrappedBlob = await _crypto.WrapKeyAsync(newTeamSecret, pub);
            newKeyShares.Add(new { MemberId = memberId, WrappedKey = Convert.ToBase64String(wrappedBlob) });
        }

        var res = await _http.PostAsJsonAsync(
            $"api/Team/{teamId}/members/{Uri.EscapeDataString(removedMemberId)}/remove",
            new { NewKeyShares = newKeyShares });
        if (!res.IsSuccessStatusCode) return false;

        _keyCache.Put(teamId, currentGeneration + 1, newTeamSecret);
        return true;
    }

    // ---------- Update Team ----------
    public async Task<Result<TeamDTOPublic>> UpdateTeamAsync(Guid teamId, TeamUpdateDTO dto)
    {
        try
        {
            var res = await _http.PatchAsJsonAsync($"api/team/{teamId}", dto);
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "Failed to update team.");

            var team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
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
            var res = await _http.DeleteAsync($"api/team/{teamId}");
            var body = await res.Content.ReadAsStringAsync();

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
    public record TeamDetailDTO(Guid Id, string Name, string Slug, string Glyph, string Color, string MessageLifetime, bool IsDirect, List<MemberDTOPublic>? Members);

    public async Task<Result<TeamDTOPublic>> GetTeamByTokenAsync(string token)
    {
        var res = await _http.GetAsync($"api/team/by-token/{token}");
        if (!res.IsSuccessStatusCode)
            return Result<TeamDTOPublic>.Fail($"Team not found ({res.StatusCode}).");

        var body = await res.Content.ReadAsStringAsync();
        var team = JsonSerializer.Deserialize<TeamDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return team != null
            ? Result<TeamDTOPublic>.Ok(team)
            : Result<TeamDTOPublic>.Fail("Invalid response.");
    }

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

    // ---------- Transfer Ownership ----------
    public async Task<Result> TransferOwnershipAsync(Guid teamId, string newOwnerId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"api/team/{teamId}/owner", new { UserId = newOwnerId });
            var body = await res.Content.ReadAsStringAsync();

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
    public async Task<Result> DemoteFromAdminAsync(Guid teamId, string userId)
    {
        try
        {
            var res = await _http.DeleteAsync($"api/team/{teamId}/admins/{userId}");
            var body = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return Result.Fail(ParseMessage(body) ?? "Failed to demote member.");

            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail("Unexpected error.");
        }
    }

    // ---------- Get or Create DM ----------
    // True E2E: the server can't generate the Team.Secret for a fresh DM (it
    // would have to read plaintext). Instead we always send wrapped shares for
    // both members. If a DM already exists the server ignores them and just
    // returns the existing row; otherwise it persists the shares atomically
    // with the team so neither user gets stuck without keys.
    public async Task<Result<TeamDTOPublic>> GetOrCreateDirectMessageAsync(string myUserId, string friendId)
    {
        Console.WriteLine($"[DM] GetOrCreateDirectMessageAsync start. myUserId={myUserId}, friendId={friendId}");
        try
        {
            var myPubKeys = await _userClient.GetPublicKeysAsync(myUserId);
            Console.WriteLine($"[DM] myPubKeys: {(myPubKeys == null ? "NULL" : "ok")}");
            var friendPubKeys = await _userClient.GetPublicKeysAsync(friendId);
            Console.WriteLine($"[DM] friendPubKeys: {(friendPubKeys == null ? "NULL" : "ok")}");
            if (myPubKeys == null || friendPubKeys == null)
                return Result<TeamDTOPublic>.Fail("Cannot fetch public keys for DM bootstrap.");

            byte[] myPub = Convert.FromBase64String(myPubKeys.EncryptionPublicKey);
            byte[] friendPub = Convert.FromBase64String(friendPubKeys.EncryptionPublicKey);
            Console.WriteLine($"[DM] pubkey bytes: myPub={myPub.Length} friendPub={friendPub.Length}");

            byte[] teamSecret = await _crypto.GenerateTeamSecretAsync();
            Console.WriteLine($"[DM] teamSecret generated, len={teamSecret.Length}");
            byte[] myWrapped = await _crypto.WrapKeyAsync(teamSecret, myPub);
            Console.WriteLine($"[DM] wrapped for me, len={myWrapped.Length}");
            byte[] friendWrapped = await _crypto.WrapKeyAsync(teamSecret, friendPub);
            Console.WriteLine($"[DM] wrapped for friend, len={friendWrapped.Length}");

            var payload = new
            {
                MyWrappedKey = Convert.ToBase64String(myWrapped),
                FriendWrappedKey = Convert.ToBase64String(friendWrapped)
            };

            Console.WriteLine($"[DM] POSTing api/team/dm/{friendId}");
            var res = await _http.PostAsJsonAsync($"api/team/dm/{friendId}", payload);
            var body = await res.Content.ReadAsStringAsync();
            Console.WriteLine($"[DM] POST response status={(int)res.StatusCode}, bodyLen={body.Length}");

            if (!res.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DM] POST FAILED. body={body}");
                return Result<TeamDTOPublic>.Fail(ParseMessage(body) ?? "Failed to create direct message.");
            }

            var dm = JsonSerializer.Deserialize<TeamDTOPublic>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dm == null)
            {
                Console.WriteLine("[DM] deserialized DM == null");
                return Result<TeamDTOPublic>.Fail("Invalid response.");
            }

            Console.WriteLine($"[DM] DM created/fetched: id={dm.Id} name={dm.Name}");
            _keyCache.Put(dm.Id, 1, teamSecret);
            Console.WriteLine($"[DM] secret cached at gen 1 for {dm.Id}");

            return Result<TeamDTOPublic>.Ok(dm);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DM] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return Result<TeamDTOPublic>.Fail($"Unexpected error: {ex.Message}");
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
