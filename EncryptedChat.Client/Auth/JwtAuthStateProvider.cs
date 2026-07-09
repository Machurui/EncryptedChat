using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using EncryptedChat.Client.Services;

namespace EncryptedChat.Client.Auth;

public class JwtAuthStateProvider(TokenStore store, TokenStorageService storage) : AuthenticationStateProvider
{
    private readonly TokenStore _store = store;
    private readonly TokenStorageService _storage = storage;
    private Task? _initTask;
    private readonly object _lock = new();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureInitializedAsync();
        ClaimsIdentity identity = BuildIdentity(_store.AccessToken, _store.ExpiresUtc);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private Task EnsureInitializedAsync()
    {
        lock (_lock) return _initTask ??= InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_store.AccessToken is null)
        {
            (string? token, DateTime? exp) = await _storage.LoadAsync();
            if (!string.IsNullOrWhiteSpace(token) && exp.HasValue && exp.Value > DateTime.UtcNow)
                _store.Set(token, exp.Value);
            else if (!string.IsNullOrWhiteSpace(token))
                await _storage.ClearAsync();
        }
    }

    private static ClaimsIdentity BuildIdentity(string? token, DateTime? expUtc)
    {
        if (string.IsNullOrWhiteSpace(token) || expUtc is not DateTime t || DateTime.UtcNow >= t)
            return new ClaimsIdentity();

        return new ClaimsIdentity(ParseJwtClaims(token), "jwt");
    }

    private static IEnumerable<Claim> ParseJwtClaims(string token)
    {
        string[] parts = token.Split('.');
        if (parts.Length < 2) 
            yield break;

        byte[] payload = Base64UrlDecode(parts[1]);
        using JsonDocument doc = JsonDocument.Parse(payload);
        JsonElement root = doc.RootElement;

        string? S(string n) => root.TryGetProperty(n, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        string? sub = S("sub");
        string? name = S("unique_name") ?? S("name");
        string? nameId = S("nameid") ?? sub;

        if (!string.IsNullOrEmpty(nameId)) yield return new Claim(ClaimTypes.NameIdentifier, nameId);
        if (!string.IsNullOrEmpty(name)) yield return new Claim(ClaimTypes.Name, name);
        if (!string.IsNullOrEmpty(sub)) yield return new Claim("sub", sub);

        if (root.TryGetProperty("role", out JsonElement roleEl)) foreach (string r in ReadStrOrArray(roleEl)) yield return new Claim(ClaimTypes.Role, r);
        else if (root.TryGetProperty("roles", out JsonElement rolesEl)) foreach (string r in ReadStrOrArray(rolesEl)) yield return new Claim(ClaimTypes.Role, r);
    }

    private static IEnumerable<string> ReadStrOrArray(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
            foreach (JsonElement i in el.EnumerateArray()) if (i.ValueKind == JsonValueKind.String) yield return i.GetString()!;
                else if (el.ValueKind == JsonValueKind.String) yield return el.GetString()!;
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
