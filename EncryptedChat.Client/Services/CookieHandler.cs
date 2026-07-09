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

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !IsRefreshRequest(request))
        {
            if (await TryRefreshTokenAsync(request, cancellationToken))
            {
                HttpRequestMessage retryRequest = await CloneRequestAsync(request);
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

            string baseUri = originalRequest.RequestUri?.GetLeftPart(UriPartial.Authority) ?? "";
            Uri refreshUri = new($"{baseUri}/api/auth/refresh");

            HttpRequestMessage refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUri)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            refreshRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            HttpResponseMessage response = await base.SendAsync(refreshRequest, cancellationToken);
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
        HttpRequestMessage clone = new(request.Method, request.RequestUri);

        if (request.Content != null)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            if (request.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
