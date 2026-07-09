using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace EncryptedChat.Client.Auth;

public class CookieAuthStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private readonly HttpClient _http = http;
    private AuthenticationState? _cachedState;
    private readonly object _lock = new();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        lock (_lock)
        {
            if (_cachedState is not null)
                return _cachedState;
        }

        AuthenticationState state = await FetchAuthStateAsync();

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
            HttpResponseMessage response = await _http.GetAsync("api/user/me");

            if (!response.IsSuccessStatusCode)
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            MeResponse? me = await response.Content.ReadFromJsonAsync<MeResponse>();
            if (me is null || string.IsNullOrEmpty(me.Id))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, me.Id),
                new("sub", me.Id)
            ];

            if (!string.IsNullOrEmpty(me.Name))
                claims.Add(new Claim(ClaimTypes.Name, me.Name));

            ClaimsIdentity identity = new(claims, "cookie");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private record MeResponse(string? Id, string? Name, string? Email, int Level);
}
