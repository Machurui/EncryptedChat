using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using EncryptedChat.Client.Services;

namespace EncryptedChat.Client.Auth;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStore _store;
    private readonly TokenStorageService _storage;
    private Task? _initTask;
    private readonly object _lock = new();

    public JwtAuthStateProvider(TokenStore store, TokenStorageService storage)
    { _store = store; _storage = storage; }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureInitializedAsync();
        var identity = BuildIdentity(_store.AccessToken, _store.ExpiresUtc);
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
            var (token, exp) = await _storage.LoadAsync();
            if (!string.IsNullOrWhiteSpace(token) && exp.HasValue && exp.Value > DateTime.UtcNow)
                _store.Set(token, exp.Value);
            else if (!string.IsNullOrWhiteSpace(token))
                await _storage.ClearAsync(); // stale
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
        var parts = token.Split('.');
        if (parts.Length < 2) yield break;
        var payload = Base64UrlDecode(parts[1]);
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        string? S(string n) => root.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var sub = S("sub");
        var name = S("unique_name") ?? S("name");
        var nameId = S("nameid") ?? sub;

        if (!string.IsNullOrEmpty(nameId)) yield return new Claim(ClaimTypes.NameIdentifier, nameId);
        if (!string.IsNullOrEmpty(name))   yield return new Claim(ClaimTypes.Name, name);
        if (!string.IsNullOrEmpty(sub))    yield return new Claim("sub", sub);

        if (root.TryGetProperty("role", out var roleEl)) foreach (var r in ReadStrOrArray(roleEl)) yield return new Claim(ClaimTypes.Role, r);
        else if (root.TryGetProperty("roles", out var rolesEl)) foreach (var r in ReadStrOrArray(rolesEl)) yield return new Claim(ClaimTypes.Role, r);
    }

    private static IEnumerable<string> ReadStrOrArray(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
            foreach (var i in el.EnumerateArray()) if (i.ValueKind == JsonValueKind.String) yield return i.GetString()!;
        else if (el.ValueKind == JsonValueKind.String) yield return el.GetString()!;
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
