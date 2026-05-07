using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using EncryptedChat.Client.Auth;

namespace EncryptedChat.Client.Services;

public class AuthClient
{
    private readonly HttpClient _http;
    private readonly CookieAuthStateProvider _authState;

    public AuthClient(HttpClient http, AuthenticationStateProvider authState)
    {
        _http = http;
        _authState = authState as CookieAuthStateProvider
            ?? throw new InvalidOperationException("CookieAuthStateProvider is not registered correctly.");
    }

    // DTO match API
    public record LoginDTO(string Email, string Password);
    public record RegisterDTO(string Email, string Password, string Name);
    public record ForgotPasswordDTO(string Email);
    public record ResetPasswordDTO(string Email, string Token, string NewPassword);

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    // ---------- Auth ----------
    public async Task<Result> LoginAsync(string email, string password)
    {
        var res = await _http.PostAsJsonAsync("api/auth/login", new LoginDTO(email, password));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "Login failed.");

        // Cookie is set automatically by the browser
        _authState.NotifyChanged();
        return Result.Ok();
    }

    public async Task LogoutAsync()
    {
        await _http.PostAsync("api/auth/logout", null);
        _authState.NotifyChanged();
    }

    // If your API has refresh implemented, call this when near expiry
    public async Task<Result> RefreshAsync(string refreshToken)
    {
        var res = await _http.PostAsJsonAsync("api/auth/refresh", new { refreshToken });
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "Refresh failed.");

        // Cookie is set automatically by the browser
        _authState.NotifyChanged();
        return Result.Ok();
    }

    // ---------- Registration ----------
    public Task<Result> RegisterAsync(RegisterDTO dto)
        => RegisterAsync(dto.Email, dto.Password, dto.Name);

    public async Task<Result> RegisterAsync(string email, string password, string name, bool autoLogin = false)
    {
        var res = await _http.PostAsJsonAsync("api/auth/register", new RegisterDTO(email, password, name));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "Registration failed.");

        if (autoLogin)
            return await LoginAsync(email, password);

        return Result.Ok();
    }

    // ---------- Password ----------
    public async Task<Result> ForgotPasswordAsync(string email)
    {
        var res = await _http.PostAsJsonAsync("api/auth/forgot-password", new ForgotPasswordDTO(email));
        var body = await res.Content.ReadAsStringAsync();
        return res.IsSuccessStatusCode ? Result.Ok() : Result.Fail(ParseMessage(body) ?? "Failed to send reset email.");
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var res = await _http.PostAsJsonAsync("api/auth/reset-password", new ResetPasswordDTO(email, token, newPassword));
        var body = await res.Content.ReadAsStringAsync();
        return res.IsSuccessStatusCode ? Result.Ok() : Result.Fail(ParseMessage(body) ?? "Password reset failed.");
    }

    // ---------- SignalR Token ----------
    public async Task<string?> GetSignalRTokenAsync()
    {
        try
        {
            var res = await _http.GetAsync("api/auth/signalr-token");
            if (!res.IsSuccessStatusCode)
                return null;

            var response = await res.Content.ReadFromJsonAsync<SignalRTokenResponse>();
            return response?.Token;
        }
        catch
        {
            return null;
        }
    }

    private record SignalRTokenResponse(string? Token);

    // ---------- Helpers ----------
    private static string? ParseMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("message", out var msg)) return null;

            return msg.ValueKind switch
            {
                JsonValueKind.String => msg.GetString(),
                JsonValueKind.Array  => string.Join("\n",
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
