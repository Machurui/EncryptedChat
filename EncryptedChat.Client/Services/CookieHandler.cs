using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace EncryptedChat.Client.Services;

public class CookieHandler : DelegatingHandler
{
    public CookieHandler() : base(new HttpClientHandler())
    {
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
