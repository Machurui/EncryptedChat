using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using EncryptedChat.Client.Auth;

namespace EncryptedChat.Client.Services;

public class AuthClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _store;
    private readonly JwtAuthStateProvider _authState;
    private readonly TokenStorageService _storage;

    public AuthClient(HttpClient http, TokenStore store, AuthenticationStateProvider authState, TokenStorageService storage)
    {
        _http = http;
        _store = store;
        _storage = storage;
        _authState = authState as JwtAuthStateProvider
            ?? throw new InvalidOperationException("JwtAuthStateProvider is not registered correctly.");
    }

    // DTO match API
    public record LoginDTO(string Email, string Password);
    public record RegisterDTO(string Email, string Password, string Name);
    public record ForgotPasswordDTO(string Email);
    public record ResetPasswordDTO(string Email, string Token, string NewPassword);

    public record LoginResponse(string accessToken, DateTime expiresUtc, string? refreshToken);

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    // ---------- Optional bootstrapping ----------
    // If you want to force-restore before first use (e.g., from a layout),
    // you can call this once on app start. The JwtAuthStateProvider + BearerHandler
    // already handle restore lazily, but this is handy if needed.
    // public async Task RestoreAsync()
    // {
    //     if (string.IsNullOrWhiteSpace(_store.AccessToken))
    //     {
    //         var (token, exp) = await _storage.LoadAsync();
    //         if (!string.IsNullOrWhiteSpace(token) && exp.HasValue && exp.Value > DateTime.UtcNow)
    //         {
    //             _store.Set(token, exp.Value);
    //             _authState.NotifyChanged();
    //         }
    //     }
    // }

    // ---------- Auth ----------
    public async Task<Result> LoginAsync(string email, string password)
    {
        var res = await _http.PostAsJsonAsync("api/auth/login", new LoginDTO(email, password));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "Login failed.");

        var dto = await res.Content.ReadFromJsonAsync<LoginResponse>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.accessToken))
            return Result.Fail("No token received.");

        _store.Set(dto.accessToken, dto.expiresUtc);
        await _storage.SaveAsync(dto.accessToken, dto.expiresUtc); // persist
        _authState.NotifyChanged();
        return Result.Ok();
    }

    public async Task LogoutAsync()
    {
        _store.Clear();
        await _storage.ClearAsync();
        _authState.NotifyChanged();
    }

    // If your API has refresh implemented, call this when near expiry
    public async Task<Result> RefreshAsync(string refreshToken)
    {
        var res = await _http.PostAsJsonAsync("api/auth/refresh", new { refreshToken });
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result.Fail(ParseMessage(body) ?? "Refresh failed.");

        var dto = await res.Content.ReadFromJsonAsync<LoginResponse>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.accessToken))
            return Result.Fail("No token received.");

        _store.Set(dto.accessToken, dto.expiresUtc);
        await _storage.SaveAsync(dto.accessToken, dto.expiresUtc);
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

    // ---------- Password flows (optional) ----------
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
