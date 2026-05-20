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
            var response = await _http.GetAsync("api/user/me");

            if (!response.IsSuccessStatusCode)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var me = await response.Content.ReadFromJsonAsync<MeResponse>();
            if (me is null || string.IsNullOrEmpty(me.Id))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, me.Id),
                new("sub", me.Id)
            };

            if (!string.IsNullOrEmpty(me.Name))
                claims.Add(new Claim(ClaimTypes.Name, me.Name));

            var identity = new ClaimsIdentity(claims, "cookie");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private record MeResponse(string? Id, string? Name, string? Email, int Level);
}
