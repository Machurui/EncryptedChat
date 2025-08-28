using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using static Microsoft.AspNetCore.Components.WebAssembly.Http.BrowserRequestCredentials;
using System.ComponentModel;
using System.ComponentModel.Design;

namespace EncryptedChat.Client.Auth;

public class CookieAuthStateProvider(HttpClient http) : AuthenticationStateProvider
{
    private readonly HttpClient _http = http;
    private volatile bool _knownAuthState;
    private volatile bool _isAuthenticated;

    // Check the current user
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_knownAuthState)
        {
            _isAuthenticated = await PingRefreshAsync();
            _knownAuthState = true;
        }

        var identity = _isAuthenticated
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Authenticated") }, "cookie")
            : new ClaimsIdentity();

        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    // Add the auth state
    public async Task<bool> MarkAuthenticatedAsync()
    {
        _isAuthenticated = await PingRefreshAsync();
        _knownAuthState = true;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return _isAuthenticated;
    }

    // Clear the auth state
    public void MarkLoggedOut()
    {
        _isAuthenticated = false;
        _knownAuthState = true;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    // Check the auth state
    private async Task<bool> PingRefreshAsync()
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh");
            req.SetBrowserRequestCredentials(Include);
            var res = await _http.SendAsync(req);
            return res.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }
}
