using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public class SecurityClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public record SessionInfo(
        Guid Id,
        string DeviceInfo,
        string DeviceKind,
        string? Location,
        string? IpAddress,
        DateTime CreatedAt,
        DateTime LastActiveAt,
        bool IsCurrent
    );

    public record SessionListResponse(int TotalCount, List<SessionInfo> Sessions);

    public record PasswordInfo(DateTime? ChangedAt);

    public record RecoveryInfo(DateTime? LastViewed);

    public record RecoveryPhrase(List<string> Words, DateTime GeneratedAt);

    public async Task<Result<SessionListResponse>> GetSessionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Security/sessions");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SessionListResponse>();
                return Result<SessionListResponse>.Ok(result!);
            }
            return Result<SessionListResponse>.Fail("Failed to load sessions");
        }
        catch (Exception ex)
        {
            return Result<SessionListResponse>.Fail(ex.Message);
        }
    }

    public async Task<Result<bool>> RevokeSessionAsync(Guid sessionId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/Security/sessions/{sessionId}");
            return response.IsSuccessStatusCode
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail("Failed to revoke session");
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }

    public async Task<Result<int>> RevokeAllOtherSessionsAsync()
    {
        try
        {
            var response = await _httpClient.DeleteAsync("api/Security/sessions");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RevokeAllResponse>();
                return Result<int>.Ok(result?.RevokedCount ?? 0);
            }
            return Result<int>.Fail("Failed to revoke sessions");
        }
        catch (Exception ex)
        {
            return Result<int>.Fail(ex.Message);
        }
    }

    private record RevokeAllResponse(int RevokedCount, string Message);

    public async Task<Result<PasswordInfo>> GetPasswordInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Security/password/info");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<PasswordInfo>();
                return Result<PasswordInfo>.Ok(result!);
            }
            return Result<PasswordInfo>.Fail("Failed to load password info");
        }
        catch (Exception ex)
        {
            return Result<PasswordInfo>.Fail(ex.Message);
        }
    }

    public async Task<Result<bool>> ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Security/password", new
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword,
                ConfirmPassword = confirmPassword
            });

            if (response.IsSuccessStatusCode)
            {
                return Result<bool>.Ok(true);
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            return Result<bool>.Fail(error?.Message ?? "Failed to change password");
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(ex.Message);
        }
    }

    private record ErrorResponse(string Message, List<string>? Errors);

    public async Task<Result<RecoveryInfo>> GetRecoveryInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Security/recovery/info");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RecoveryInfo>();
                return Result<RecoveryInfo>.Ok(result!);
            }
            return Result<RecoveryInfo>.Fail("Failed to load recovery info");
        }
        catch (Exception ex)
        {
            return Result<RecoveryInfo>.Fail(ex.Message);
        }
    }

    public async Task<Result<RecoveryPhrase>> GetRecoveryPhraseAsync(string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Security/recovery", new { Password = password });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RecoveryPhrase>();
                return Result<RecoveryPhrase>.Ok(result!);
            }
            return Result<RecoveryPhrase>.Fail("Invalid password");
        }
        catch (Exception ex)
        {
            return Result<RecoveryPhrase>.Fail(ex.Message);
        }
    }

    public async Task<Result<RecoveryPhrase>> RegenerateRecoveryPhraseAsync(string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Security/recovery/regenerate", new { Password = password });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RecoveryPhrase>();
                return Result<RecoveryPhrase>.Ok(result!);
            }
            return Result<RecoveryPhrase>.Fail("Failed to regenerate recovery phrase");
        }
        catch (Exception ex)
        {
            return Result<RecoveryPhrase>.Fail(ex.Message);
        }
    }
}

public class Result<T>
{
    public bool Success { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail(string error) => new() { Success = false, Error = error };
}
