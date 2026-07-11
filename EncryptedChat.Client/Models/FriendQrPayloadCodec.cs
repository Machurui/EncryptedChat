using System.Text.Json;
using System.Text.Json.Serialization;

namespace EncryptedChat.Client.Models;

public static class FriendQrPayloadCodec
{
    private const string PayloadType = "encryptedchat.friend";
    private const int PayloadVersion = 1;
    private const int MaxPayloadLength = 512;

    public static string Encode(string handle)
    {
        if (!TryNormalizeHandle(handle, out string normalizedHandle))
            throw new ArgumentException("Handle must contain 3 to 32 letters, digits, or underscores.", nameof(handle));

        return JsonSerializer.Serialize(new Payload(PayloadType, PayloadVersion, normalizedHandle));
    }

    public static bool TryDecode(string? rawPayload, out string handle)
    {
        handle = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPayload) || rawPayload.Length > MaxPayloadLength)
            return false;

        string candidate = rawPayload.Trim();
        if (candidate.StartsWith('@'))
            candidate = candidate[1..];

        if (TryNormalizeHandle(candidate, out handle))
            return true;

        try
        {
            using JsonDocument document = JsonDocument.Parse(candidate);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out JsonElement type)
                || type.ValueKind != JsonValueKind.String
                || !string.Equals(type.GetString(), PayloadType, StringComparison.Ordinal)
                || !root.TryGetProperty("version", out JsonElement version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out int parsedVersion)
                || parsedVersion != PayloadVersion
                || !root.TryGetProperty("handle", out JsonElement encodedHandle)
                || encodedHandle.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return TryNormalizeHandle(encodedHandle.GetString(), out handle);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryNormalizeHandle(string? value, out string handle)
    {
        handle = string.Empty;
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length is < 3 or > 32)
            return false;

        if (!candidate.All(character =>
            character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '_'))
        {
            return false;
        }

        handle = candidate;
        return true;
    }

    private sealed record Payload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("handle")] string Handle);
}
