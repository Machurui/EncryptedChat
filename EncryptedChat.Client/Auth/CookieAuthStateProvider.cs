using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace EncryptedChat.Client.Auth;

public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;
    private AuthenticationState? _cachedState;
    private readonly object _lock = new();

    public CookieAuthStateProvider(HttpClient http)
    {
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        lock (_lock)
        {
            if (_cachedState is not null)
                return _cachedState;
        }

        var state = await FetchAuthStateAsync();

        lock (_lock)
        {
            _cachedState = state;
        }

        return state;
    }

    public void NotifyChanged()
    {
        lock (_lock)
        {
            _cachedState = null;
        }
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<AuthenticationState> FetchAuthStateAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/auth/me");

            if (!response.IsSuccessStatusCode)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var me = await response.Content.ReadFromJsonAsync<MeResponse>();
            if (me is null || string.IsNullOrEmpty(me.UserId))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, me.UserId),
                new("sub", me.UserId)
            };

            if (!string.IsNullOrEmpty(me.Name))
                claims.Add(new Claim(ClaimTypes.Name, me.Name));

            if (me.Roles is not null)
            {
                foreach (var role in me.Roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, "cookie");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private record MeResponse(string? UserId, string? Name, List<string>? Roles, bool IsAuthenticated);
}
