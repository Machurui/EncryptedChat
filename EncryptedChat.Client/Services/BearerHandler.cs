using System.Net.Http.Headers;

namespace EncryptedChat.Client.Services;

public class BearerHandler : DelegatingHandler
{
    private readonly TokenStore _store;
    private readonly TokenStorageService _storage;
    private bool _loaded;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BearerHandler(TokenStore store, TokenStorageService storage)
    { _store = store; _storage = storage; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        if (!_loaded && string.IsNullOrWhiteSpace(_store.AccessToken))
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (!_loaded && string.IsNullOrWhiteSpace(_store.AccessToken))
                {
                    var (tk, exp) = await _storage.LoadAsync();
                    if (!string.IsNullOrWhiteSpace(tk) && exp.HasValue && exp.Value > DateTime.UtcNow)
                        _store.Set(tk, exp.Value);
                    _loaded = true;
                }
            }
            finally { _gate.Release(); }
        }

        if (!string.IsNullOrWhiteSpace(_store.AccessToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _store.AccessToken);

        return await base.SendAsync(req, ct);
    }
}
