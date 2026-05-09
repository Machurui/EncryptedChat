using System.Net;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace EncryptedChat.Client.Services;

public class CookieHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static bool _isRefreshing;

    public CookieHandler() : base(new HttpClientHandler())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !IsRefreshRequest(request))
        {
            if (await TryRefreshTokenAsync(request, cancellationToken))
            {
                var retryRequest = await CloneRequestAsync(request);
                retryRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                response = await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
    }

    private static bool IsRefreshRequest(HttpRequestMessage request)
    {
        return request.RequestUri?.PathAndQuery.Contains("auth/refresh", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<bool> TryRefreshTokenAsync(HttpRequestMessage originalRequest, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRefreshing)
                return false;

            _isRefreshing = true;

            var baseUri = originalRequest.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "";
            var refreshUri = new Uri($"{baseUri}/api/auth/refresh");

            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUri)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            refreshRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            var response = await base.SendAsync(refreshRequest, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isRefreshing = false;
            _refreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content != null)
        {
            var content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            if (request.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
