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
    public record RegisterDTO(string Email, string Password, string Handle);
    public record ForgotPasswordDTO(string Email);
    public record ResetPasswordDTO(string Email, string Token, string NewPassword);

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public class Result<T>
    {
        public bool Success { get; init; }
        public T? Value { get; init; }
        public string? ErrorMessage { get; init; }
        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    // Result payloads for recovery-phrase flows
    public record RegisterResult(string Message, IReadOnlyList<string> RecoveryWords, string AccessToken);
    public record RecoverResult(string Message, IReadOnlyList<string> NewRecoveryWords, string AccessToken);

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
    public Task<Result<RegisterResult>> RegisterAsync(RegisterDTO dto)
        => RegisterAsync(dto.Email, dto.Password, dto.Handle);

    public async Task<Result<RegisterResult>> RegisterAsync(string email, string password, string handle)
    {
        var res = await _http.PostAsJsonAsync("api/auth/register", new RegisterDTO(email, password, handle));
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<RegisterResult>.Fail(ParseMessage(body) ?? "Registration failed.");

        // Parse the new { message, recoveryWords } response shape.
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() ?? "Registered"
                : "Registered";

            var words = new List<string>();
            if (root.TryGetProperty("recoveryWords", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in arr.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var w = element.GetString();
                        if (!string.IsNullOrEmpty(w)) words.Add(w);
                    }
                }
            }

            string accessToken = root.TryGetProperty("accessToken", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() ?? ""
                : "";

            return Result<RegisterResult>.Ok(new RegisterResult(message, words, accessToken));
        }
        catch
        {
            return Result<RegisterResult>.Fail("Registration succeeded but response could not be parsed.");
        }
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

    // ---------- Recovery ----------
    public async Task<Result<RecoverResult>> RecoverAsync(string email, IReadOnlyList<string> words, string newPassword)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("api/auth/recover", new
            {
                Email = email,
                Words = words,
                NewPassword = newPassword
            });

            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    string message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString() ?? "Recovery successful"
                        : "Recovery successful";

                    var newWords = new List<string>();
                    if (root.TryGetProperty("newRecoveryWords", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in arr.EnumerateArray())
                        {
                            if (element.ValueKind == JsonValueKind.String)
                            {
                                var w = element.GetString();
                                if (!string.IsNullOrEmpty(w)) newWords.Add(w);
                            }
                        }
                    }

                    string accessToken = root.TryGetProperty("accessToken", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString() ?? ""
                        : "";

                    return Result<RecoverResult>.Ok(new RecoverResult(message, newWords, accessToken));
                }
                catch
                {
                    return Result<RecoverResult>.Fail("Recovery succeeded but response could not be parsed.");
                }
            }

            if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                return Result<RecoverResult>.Fail("Too many recovery attempts. Try again later.");

            var errBody = await res.Content.ReadAsStringAsync();
            return Result<RecoverResult>.Fail(ParseMessage(errBody) ?? "Invalid email or recovery phrase.");
        }
        catch (HttpRequestException)
        {
            return Result<RecoverResult>.Fail("Network error");
        }
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

    // ---------- E2E identity-key endpoints (real bodies land in Task 10) ----------

    public record EncryptionKeysResponse(
        string? SigningPublicKey,
        string? EncryptionPublicKey,
        string? EncryptedKeyBundle,
        string? KeyBundleSalt);

    public async Task<EncryptionKeysResponse?> GetMyEncryptionKeysAsync()
    {
        var res = await _http.GetAsync("api/User/me/encryption-keys");
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<EncryptionKeysResponse>();
    }

    public async Task<bool> SetEncryptionKeysAsync(
        string signingPublicKey, string encryptionPublicKey,
        string encryptedKeyBundle, string keyBundleSalt)
    {
        var res = await _http.PutAsJsonAsync("api/User/me/encryption-keys", new
        {
            SigningPublicKey = signingPublicKey,
            EncryptionPublicKey = encryptionPublicKey,
            EncryptedKeyBundle = encryptedKeyBundle,
            KeyBundleSalt = keyBundleSalt
        });
        return res.IsSuccessStatusCode;
    }
}
