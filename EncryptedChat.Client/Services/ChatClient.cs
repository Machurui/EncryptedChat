using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;

namespace EncryptedChat.Client.Services;

public class ChatClient
{
    private readonly HttpClient _http;
    private readonly CryptoService _crypto;
    private readonly KeyVaultService _vault;
    private readonly TeamKeyCacheService _keyCache;
    private readonly UserClient _userClient;
    private readonly ConcurrentDictionary<string, UserClient.PublicKeysResponse> _senderPubKeyCache = new();

    public class MessageDTOPublic
    {
        public Guid Id { get; set; }
        // E2E: server stores+returns the encrypted envelope. UI reads DisplayText
        // after calling DecryptMessageAsync.
        public string EncryptedText { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int KeyGeneration { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public SenderDTO? Sender { get; set; }
        public Guid TeamId { get; set; }
        public DateTime Date { get; set; }
        public List<AttachmentClient.AttachmentDTOPublic>? Attachments { get; set; }

        // Runtime-only, populated by DecryptMessageAsync. Not serialized.
        [System.Text.Json.Serialization.JsonIgnore]
        public string? DisplayText { get; set; }

        // TEMP-Task14: shim for existing Chat.razor markup that still reads
        // `msg.Text`. Task 14 rewires the UI to read DisplayText directly and
        // this property goes away.
        [System.Text.Json.Serialization.JsonIgnore]
        public string? Text => DisplayText;
    }

    public class SenderDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Handle { get; set; }
        public string NameColor { get; set; } = "#FFFFFF";
        public string? ProfileImageUrl { get; set; }
    }

    public ChatClient(HttpClient http, CryptoService crypto, KeyVaultService vault, TeamKeyCacheService keyCache, UserClient userClient)
    {
        _http = http;
        _crypto = crypto;
        _vault = vault;
        _keyCache = keyCache;
        _userClient = userClient;
    }

    public class Result<T>
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public T? Value { get; init; }
        public bool RateLimited { get; init; }
        public int RetryAfterMs { get; init; }

        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
        public static Result<T> Throttled(int retryAfterMs) =>
            new() { Success = false, RateLimited = true, RetryAfterMs = retryAfterMs, ErrorMessage = "Rate limited" };
    }

    private record RateLimitedResponse(int RetryAfterMs);

    // TEMP-Task14: legacy plaintext-send shim so Chat.razor compiles. Always
    // fails — the UI must migrate to SendMessageAsync(teamId, plaintext, gen, senderId)
    // in Task 14 once Chat.razor knows the team's current generation + caller id.
    public Task<Result<MessageDTOPublic>> CreateMessageAsync(Guid teamId, string text)
        => Task.FromResult(Result<MessageDTOPublic>.Fail(
            "Legacy plaintext send disabled in True E2E v1. Call SendMessageAsync(teamId, text, gen, senderId) instead."));

    public async Task<Result<List<MessageDTOPublic>>> GetMessagesByTeamAsync(
        Guid teamId, int page = 1, int pageSize = 20)
    {
        var res = await _http.GetAsync($"api/Message/team/{teamId}?page={page}&pageSize={pageSize}");
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            return Result<List<MessageDTOPublic>>.Fail($"Failed to load messages ({res.StatusCode}).");
        }

        var msgs = JsonSerializer.Deserialize<List<MessageDTOPublic>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return Result<List<MessageDTOPublic>>.Ok(msgs);
    }

    // E2E: encrypts + signs client-side, posts the envelope. Server stores blob.
    public async Task<Result<MessageDTOPublic>> SendMessageAsync(Guid teamId, string plaintext, int teamGeneration, string senderId)
    {
        var stored = await _vault.GetMyKeysAsync(senderId);
        if (stored == null)
            return Result<MessageDTOPublic>.Fail("Encryption keys not available on this device. Bootstrap via recovery phrase.");

        byte[]? teamSecret = _keyCache.Get(teamId, teamGeneration);
        if (teamSecret == null)
            return Result<MessageDTOPublic>.Fail("Team key not loaded for this generation. Open the team to load.");

        CryptoService.MessageEnvelope envelope = await _crypto.EncryptAndSignMessageAsync(
            plaintext, teamSecret, teamGeneration,
            stored.SigningPrivateKey, teamId, senderId);

        var payload = new
        {
            TeamId = teamId,
            EncryptedText = envelope.EncryptedText,
            Iv = envelope.Iv,
            Signature = envelope.Signature,
            KeyGeneration = envelope.KeyGeneration
        };
        var json = JsonSerializer.Serialize(payload);

        var req = new HttpRequestMessage(HttpMethod.Post, "api/Message")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if ((int)res.StatusCode == 429)
        {
            int retryAfterMs = 1000;
            try
            {
                var parsed = JsonSerializer.Deserialize<RateLimitedResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) retryAfterMs = parsed.RetryAfterMs;
            }
            catch { /* default 1000ms */ }
            return Result<MessageDTOPublic>.Throttled(retryAfterMs);
        }

        if (!res.IsSuccessStatusCode)
            return Result<MessageDTOPublic>.Fail($"Failed to send message ({res.StatusCode}).");

        var msg = JsonSerializer.Deserialize<MessageDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (msg is null)
            return Result<MessageDTOPublic>.Fail("Invalid response from server.");

        // Tag the locally-sent message with its plaintext so the UI doesn't need
        // a roundtrip-decrypt for messages this device just produced.
        msg.DisplayText = plaintext;

        return Result<MessageDTOPublic>.Ok(msg);
    }

    // Decrypts + verifies an inbound message. Fetches the sender's pubkey
    // (cached) for signature verification. Returns null on verify/decrypt failure
    // so the UI can mark the message tampered or undecryptable.
    public async Task<string?> DecryptMessageAsync(MessageDTOPublic message, string callerUserId)
    {
        if (!string.IsNullOrEmpty(message.DisplayText))
            return message.DisplayText;

        byte[]? teamSecret = _keyCache.Get(message.TeamId, message.KeyGeneration);
        if (teamSecret == null) return null;

        if (!_senderPubKeyCache.TryGetValue(message.SenderId, out var senderPubKeys))
        {
            senderPubKeys = await _userClient.GetPublicKeysAsync(message.SenderId);
            if (senderPubKeys != null)
                _senderPubKeyCache[message.SenderId] = senderPubKeys;
        }
        if (senderPubKeys == null) return null;

        byte[] senderSigning = Convert.FromBase64String(senderPubKeys.SigningPublicKey);

        try
        {
            string plaintext = await _crypto.DecryptAndVerifyMessageAsync(
                new CryptoService.MessageEnvelope(
                    message.EncryptedText, message.Iv, message.Signature, message.KeyGeneration),
                teamSecret,
                senderSigning,
                message.TeamId,
                message.SenderId);
            message.DisplayText = plaintext;
            return plaintext;
        }
        catch
        {
            return null;
        }
    }
}
