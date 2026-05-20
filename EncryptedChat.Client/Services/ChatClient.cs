using System.Text;
using System.Text.Json;

namespace EncryptedChat.Client.Services;

public class ChatClient
{
    private readonly HttpClient _http;

    public class MessageDTOPublic
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public SenderDTO? Sender { get; set; }
        public Guid TeamId { get; set; }
        public DateTime Date { get; set; }
        public List<AttachmentClient.AttachmentDTOPublic>? Attachments { get; set; }
    }

    public class SenderDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Handle { get; set; }
        public string NameColor { get; set; } = "#FFFFFF";
        public string? ProfileImageUrl { get; set; }
    }

    public ChatClient(HttpClient http)
    {
        _http = http;
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

    // GET api/Message/team/{teamId}?page=N&pageSize=N
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

    // POST api/Message - create message via REST (returns message with ID for attachments)
    private class MessageCreateDTO
    {
        public string Text { get; set; } = string.Empty;
        public Guid Team { get; set; }
    }

    public async Task<Result<MessageDTOPublic>> CreateMessageAsync(Guid teamId, string text)
    {
        var dto = new MessageCreateDTO { Text = text, Team = teamId };
        var json = JsonSerializer.Serialize(dto);

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
        {
            return Result<MessageDTOPublic>.Fail($"Failed to send message ({res.StatusCode}).");
        }

        var msg = JsonSerializer.Deserialize<MessageDTOPublic>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (msg is null)
            return Result<MessageDTOPublic>.Fail("Invalid response from server.");

        return Result<MessageDTOPublic>.Ok(msg);
    }
}