using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using static Microsoft.AspNetCore.Components.WebAssembly.Http.BrowserRequestCredentials;
using EncryptedChat.Client.Auth;

namespace EncryptedChat.Client.Services;

public class AuthClient(HttpClient http, CookieAuthStateProvider state)
{
    private readonly HttpClient _http = http;
    private readonly CookieAuthStateProvider _state = state;

    // DTOs match API
    public record LoginDTO(string Email, string Password);
    public record RegisterDTO(string FirstName, string LastName, string Email, string Password);
    public record ForgotPasswordDTO(string Email);
    public record ResetPasswordDTO(string Email, string Token, string NewPassword);

    // Login call
    public async Task<bool> LoginAsync(string email, string password)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
        {
            Content = JsonContent.Create(new LoginDTO(email, password))
        };
        req.SetBrowserRequestCredentials(Include);
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return false;

        return await _state.MarkAuthenticatedAsync();
    }

    // Register call
    public async Task<bool> RegisterAsync(RegisterDTO dto)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/register")
        {
            Content = JsonContent.Create(dto)
        };
        req.SetBrowserRequestCredentials(Include);
        var res = await _http.SendAsync(req);
        return res.IsSuccessStatusCode;
    }

    // Logout call
    public async Task LogoutAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        req.SetBrowserRequestCredentials(Include);
        await _http.SendAsync(req);
        _state.MarkLoggedOut();
    }

    // Refresh call
    public Task<bool> RefreshAsync() => _state.MarkAuthenticatedAsync();
}
