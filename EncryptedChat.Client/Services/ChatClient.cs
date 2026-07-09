using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using EncryptedChat.Client.Services.Crypto;
using static EncryptedChat.Client.Services.Crypto.KeyVaultService;
using static EncryptedChat.Client.Services.UserClient;

namespace EncryptedChat.Client.Services;

public class ChatClient(HttpClient http, CryptoService crypto, KeyVaultService vault, TeamKeyCacheService keyCache, UserClient userClient, IKeyVerificationService keyVerify)
{
    private readonly HttpClient _http = http;
    private readonly CryptoService _crypto = crypto;
    private readonly KeyVaultService _vault = vault;
    private readonly TeamKeyCacheService _keyCache = keyCache;
    private readonly UserClient _userClient = userClient;
    private readonly IKeyVerificationService _keyVerify = keyVerify;
    private readonly ConcurrentDictionary<string, UserClient.PublicKeysResponse> _senderPubKeyCache = new();

    public class MessageDTOPublic
    {
        public Guid Id { get; set; }
        public string EncryptedText { get; set; } = string.Empty;
        public string Iv { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int KeyGeneration { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public SenderDTO? Sender { get; set; }
        public Guid TeamId { get; set; }
        public DateTime Date { get; set; }
        public List<AttachmentClient.AttachmentDTOPublic>? Attachments { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? DisplayText { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool SenderKeyChanged { get; set; }

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

    public static Task<Result<MessageDTOPublic>> CreateMessageAsync(Guid teamId, string text)
        => Task.FromResult(Result<MessageDTOPublic>.Fail(
            "Legacy plaintext send disabled in True E2E v1. Call SendMessageAsync(teamId, text, gen, senderId) instead."));

    public async Task<Result<List<MessageDTOPublic>>> GetMessagesByTeamAsync(
        Guid teamId, int page = 1, int pageSize = 20)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/Message/team/{teamId}?page={page}&pageSize={pageSize}");
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<List<MessageDTOPublic>>.Fail($"Failed to load messages ({res.StatusCode}).");

        List<MessageDTOPublic> msgs = JsonSerializer.Deserialize<List<MessageDTOPublic>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return Result<List<MessageDTOPublic>>.Ok(msgs);
    }

    public async Task<Result<MessageDTOPublic>> SendMessageAsync(Guid teamId, string plaintext, int teamGeneration, string senderId)
    {
        StoredKeys? stored = await _vault.GetMyKeysAsync(senderId);
        if (stored == null)
            return Result<MessageDTOPublic>.Fail("Encryption keys not available on this device. Bootstrap via recovery phrase.");

        byte[]? teamSecret = _keyCache.Get(teamId, teamGeneration);
        if (teamSecret == null)
            return Result<MessageDTOPublic>.Fail("Team key not loaded for this generation. Open the team to load.");

        CryptoService.MessageEnvelope envelope = await _crypto.EncryptAndSignMessageAsync(
            plaintext, teamSecret, teamGeneration,
            stored.SigningPrivateKey, teamId, senderId);

        Payload payload = new
        (
            Team: teamId,
            EncryptedText: envelope.EncryptedText,
            Iv: envelope.Iv,
            Signature: envelope.Signature,
            KeyGeneration: envelope.KeyGeneration
        );

        string json = JsonSerializer.Serialize(payload);

        HttpRequestMessage req = new(HttpMethod.Post, "api/Message")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage res = await _http.SendAsync(req);
        string body = await res.Content.ReadAsStringAsync();

        if ((int)res.StatusCode == 429)
        {
            int retryAfterMs = 1000;
            try
            {
                RateLimitedResponse? parsed = JsonSerializer.Deserialize<RateLimitedResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed != null) retryAfterMs = parsed.RetryAfterMs;
            }
            catch { /* default 1000ms */ }
            return Result<MessageDTOPublic>.Throttled(retryAfterMs);
        }

        if (!res.IsSuccessStatusCode)
            return Result<MessageDTOPublic>.Fail($"Failed to send message ({res.StatusCode}).");

        MessageDTOPublic? msg = JsonSerializer.Deserialize<MessageDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (msg is null)
            return Result<MessageDTOPublic>.Fail("Invalid response from server.");

        msg.DisplayText = plaintext;

        return Result<MessageDTOPublic>.Ok(msg);
    }

    public async Task<string?> DecryptMessageAsync(MessageDTOPublic message, string callerUserId)
    {
        if (!string.IsNullOrEmpty(message.DisplayText))
            return message.DisplayText;

        string? senderId = !string.IsNullOrEmpty(message.SenderId)
            ? message.SenderId
            : message.Sender?.Id;
        if (string.IsNullOrEmpty(senderId)) return null;

        byte[]? teamSecret = _keyCache.Get(message.TeamId, message.KeyGeneration);
        if (teamSecret == null) return null;

        if (!_senderPubKeyCache.TryGetValue(senderId, out PublicKeysResponse? senderPubKeys))
        {
            senderPubKeys = await _userClient.GetPublicKeysAsync(senderId);
            if (senderPubKeys != null)
                _senderPubKeyCache[senderId] = senderPubKeys;
        }
        if (senderPubKeys == null) return null;

        // Non-blocking: a changed sender key is surfaced as a warning. We cannot
        // un-receive a message, but the user should be alerted to verify.
        if (await _keyVerify.CheckAndPinAsync(senderId, senderPubKeys.SigningPublicKey, senderPubKeys.EncryptionPublicKey)
                == KeyPinResult.Changed)
            message.SenderKeyChanged = true;

        byte[] senderSigning = Convert.FromBase64String(senderPubKeys.SigningPublicKey);

        try
        {
            string plaintext = await _crypto.DecryptAndVerifyMessageAsync(
                new CryptoService.MessageEnvelope(
                    message.EncryptedText, message.Iv, message.Signature, message.KeyGeneration),
                teamSecret,
                senderSigning,
                message.TeamId,
                senderId);
            message.DisplayText = plaintext;
            return plaintext;
        }
        catch
        {
            return null;
        }
    }

    public record Payload(
        Guid Team, 
        string EncryptedText, 
        string Iv, 
        string Signature, 
        int KeyGeneration 
    );
}
