using Microsoft.JSInterop;
using System.Globalization;

namespace EncryptedChat.Client.Services;

public class TokenStorageService(IJSRuntime js)
{
    private readonly IJSRuntime _js = js;
    private const string TK = "ec.accessToken";
    private const string EX = "ec.accessTokenExpires";

    public Task SaveAsync(string token, DateTime expUtc) =>
        Task.WhenAll(
            _js.InvokeVoidAsync("sessionStorage.setItem", TK, token).AsTask(),
            _js.InvokeVoidAsync("sessionStorage.setItem", EX, expUtc.ToString("o")).AsTask()
        );

    public async Task<(string? token, DateTime? exp)> LoadAsync()
    {
        string token = await _js.InvokeAsync<string>("sessionStorage.getItem", TK);
        string expS = await _js.InvokeAsync<string>("sessionStorage.getItem", EX);
        if (string.IsNullOrWhiteSpace(token)) 
            return (null, null);
            
        return (token, DateTime.TryParse(expS, null, DateTimeStyles.RoundtripKind, out DateTime dt) ? dt : null);
    }

    public Task ClearAsync() =>
        Task.WhenAll(
            _js.InvokeVoidAsync("sessionStorage.removeItem", TK).AsTask(),
            _js.InvokeVoidAsync("sessionStorage.removeItem", EX).AsTask()
        );
}
