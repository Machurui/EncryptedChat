using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;

namespace EncryptedChat.Client.Services;

// Per-user encrypted GIF vault (favorites + recents) synced across devices.
// The server stores only ciphertext + a wrapped key; all crypto/merge is here.
public sealed class GifVaultService(
    HttpClient http,
    CryptoService crypto,
    KeyVaultService keyVault,
    UserClient userClient,
    RecentGifsService localRecents)
{
    private readonly HttpClient _http = http;
    private readonly CryptoService _crypto = crypto;
    private readonly KeyVaultService _keyVault = keyVault;
    private readonly UserClient _userClient = userClient;
    private readonly RecentGifsService _localRecents = localRecents;

    private string? _userId;
    private byte[]? _vaultKey;
    private string? _wrappedKeyB64;
    private int _revision;
    private GifVaultState _state = new();
    private bool _available;
    private bool _loaded;
    private System.Threading.Timer? _debounce;

    public bool Available => _available;
    public IReadOnlyList<GifItem> Favorites => _state.Favorites;
    public IReadOnlyList<GifItem> Recents => _state.Recents;
    public bool IsFavorite(string url) => _state.Favorites.Any(f => f.Url == url);

    private record VaultReadResponse(string WrappedKey, string Iv, string Blob, int Revision);
    private record RevisionResponse(int Revision);

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task EnsureLoadedAsync(string userId)
    {
        if (_loaded) return;
        _loaded = true;
        _userId = userId;

        var keys = await _keyVault.GetMyKeysAsync(userId);
        if (keys is null) { _available = false; return; } // E2E not bootstrapped yet

        try
        {
            var resp = await _http.GetAsync("api/GifVault");
            if (resp.StatusCode == HttpStatusCode.NoContent)
            {
                await BootstrapAsync(userId);
            }
            else if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<VaultReadResponse>();
                if (dto is null) { _available = false; return; }
                _vaultKey = await _crypto.UnwrapKeyAsync(Convert.FromBase64String(dto.WrappedKey), keys.EncryptionPrivateKey);
                var json = await _crypto.DecryptAesGcmAsync(
                    Convert.FromBase64String(dto.Iv), Convert.FromBase64String(dto.Blob), _vaultKey);
                _state = JsonSerializer.Deserialize<GifVaultState>(json) ?? new();
                _wrappedKeyB64 = dto.WrappedKey;
                _revision = dto.Revision;
                _available = true;
            }
            else
            {
                _available = false;
            }
        }
        catch
        {
            // Network or decrypt failure → degraded mode. Never blind-overwrite server.
            _available = false;
        }
    }

    private async Task BootstrapAsync(string userId)
    {
        var pub = await _userClient.GetPublicKeysAsync(userId);
        if (pub is null) { _available = false; return; }

        _vaultKey = await _crypto.GenerateTeamSecretAsync(); // 32 random bytes
        var encPub = Convert.FromBase64String(pub.EncryptionPublicKey);
        _wrappedKeyB64 = Convert.ToBase64String(await _crypto.WrapKeyAsync(_vaultKey, encPub));

        // One-shot migration of LocalStorage recents (newest first → descending Ts).
        var now = NowMs();
        var local = await _localRecents.GetAllAsync();
        _state = new GifVaultState
        {
            Recents = local.Select((r, i) => new GifItem(r.Url, r.PreviewUrl, r.Width, r.Height, "gifs", now - i)).ToList()
        };
        _revision = 0;
        _available = true;
        await PushAsync();
    }

    public Task ToggleFavoriteAsync(GifItem gif)
    {
        if (!_available) return Task.CompletedTask;
        var now = NowMs();
        if (_state.Favorites.Any(f => f.Url == gif.Url))
        {
            _state.Favorites.RemoveAll(f => f.Url == gif.Url);
            _state.Tombstones.RemoveAll(t => t.Url == gif.Url);
            _state.Tombstones.Add(new GifTomb(gif.Url, now));
        }
        else
        {
            _state.Favorites.RemoveAll(f => f.Url == gif.Url);
            _state.Favorites.Insert(0, gif with { Ts = now });
            _state.Tombstones.RemoveAll(t => t.Url == gif.Url);
            if (_state.Favorites.Count > GifVaultMerge.MaxFavorites)
                _state.Favorites.RemoveRange(GifVaultMerge.MaxFavorites, _state.Favorites.Count - GifVaultMerge.MaxFavorites);
        }
        ScheduleSync();
        return Task.CompletedTask;
    }

    public Task AddRecentAsync(GifItem gif)
    {
        if (!_available)
            return _localRecents.AddAsync(new RecentGifsService.RecentGif(gif.Url, gif.PreviewUrl, gif.Width, gif.Height));

        var now = NowMs();
        _state.Recents.RemoveAll(r => r.Url == gif.Url);
        _state.Recents.Insert(0, gif with { Ts = now });
        if (_state.Recents.Count > GifVaultMerge.MaxRecents)
            _state.Recents.RemoveRange(GifVaultMerge.MaxRecents, _state.Recents.Count - GifVaultMerge.MaxRecents);
        ScheduleSync();
        return Task.CompletedTask;
    }

    private void ScheduleSync()
    {
        _debounce?.Dispose();
        _debounce = new System.Threading.Timer(async _ =>
        {
            try { await SyncAsync(); }
            catch (Exception ex) { Console.WriteLine($"[GifVault] sync error: {ex.Message}"); }
        }, null, 800, System.Threading.Timeout.Infinite);
    }

    public async Task SyncAsync()
    {
        if (!_available || _vaultKey is null) return;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await PushAsync()) return;
            if (!await MergeFromServerAsync()) return; // unrecoverable → stop
        }
        Console.WriteLine("[GifVault] sync gave up after 3 conflict retries");
    }

    private async Task<bool> PushAsync()
    {
        if (_vaultKey is null || _wrappedKeyB64 is null) return false;
        var json = JsonSerializer.SerializeToUtf8Bytes(_state);
        var enc = await _crypto.EncryptAesGcmAsync(json, _vaultKey);
        var body = new
        {
            WrappedKey = _wrappedKeyB64,
            Iv = Convert.ToBase64String(enc.Iv),
            Blob = Convert.ToBase64String(enc.CiphertextWithTag),
            ExpectedRevision = _revision
        };
        var resp = await _http.PutAsJsonAsync("api/GifVault", body);
        if (resp.StatusCode == HttpStatusCode.Conflict) return false;
        if (!resp.IsSuccessStatusCode) return false;
        var r = await resp.Content.ReadFromJsonAsync<RevisionResponse>();
        if (r is not null) _revision = r.Revision;
        return true;
    }

    private async Task<bool> MergeFromServerAsync()
    {
        if (_userId is null) return false;
        var keys = await _keyVault.GetMyKeysAsync(_userId);
        if (keys is null) return false;

        var resp = await _http.GetAsync("api/GifVault");
        if (resp.StatusCode == HttpStatusCode.NoContent) { _revision = 0; return true; }
        if (!resp.IsSuccessStatusCode) return false;

        var dto = await resp.Content.ReadFromJsonAsync<VaultReadResponse>();
        if (dto is null) return false;
        var remoteKey = await _crypto.UnwrapKeyAsync(Convert.FromBase64String(dto.WrappedKey), keys.EncryptionPrivateKey);
        var remoteJson = await _crypto.DecryptAesGcmAsync(
            Convert.FromBase64String(dto.Iv), Convert.FromBase64String(dto.Blob), remoteKey);
        var remoteState = JsonSerializer.Deserialize<GifVaultState>(remoteJson) ?? new();

        _state = GifVaultMerge.Merge(_state, remoteState, NowMs());
        _vaultKey = remoteKey;
        _wrappedKeyB64 = dto.WrappedKey;
        _revision = dto.Revision;
        return true;
    }
}
