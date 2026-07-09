using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class PinnedMessageClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public class PinnedMessageDTO
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public ChatClient.MessageDTOPublic? Message { get; set; }
        public string PinnedById { get; set; } = string.Empty;
        public string PinnedByName { get; set; } = string.Empty;
        public DateTime PinnedAt { get; set; }
    }

    public class Result
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }

        public static Result Ok() => new() { Success = true };
        public static Result Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public class Result<T>
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public T? Value { get; init; }

        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    public async Task<Result<List<PinnedMessageDTO>>> GetPinnedMessagesAsync(Guid teamId)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/team/{teamId}/pins");
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<List<PinnedMessageDTO>>.Fail($"Failed to load pinned messages ({res.StatusCode}).");

        List<PinnedMessageDTO> pins = JsonSerializer.Deserialize<List<PinnedMessageDTO>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return Result<List<PinnedMessageDTO>>.Ok(pins);
    }

    public async Task<Result<PinnedMessageDTO>> PinMessageAsync(Guid teamId, Guid messageId)
    {
        HttpResponseMessage res = await _http.PostAsync($"api/team/{teamId}/pins/{messageId}", null);
        string body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            return Result<PinnedMessageDTO>.Fail("Failed to pin message.");

        PinnedMessageDTO? pin = JsonSerializer.Deserialize<PinnedMessageDTO>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return pin != null
            ? Result<PinnedMessageDTO>.Ok(pin)
            : Result<PinnedMessageDTO>.Fail("Invalid response.");
    }

    public async Task<Result> UnpinMessageAsync(Guid teamId, Guid messageId)
    {
        HttpResponseMessage res = await _http.DeleteAsync($"api/team/{teamId}/pins/{messageId}");

        if (!res.IsSuccessStatusCode)
            return Result.Fail("Failed to unpin message.");

        return Result.Ok();
    }
}
