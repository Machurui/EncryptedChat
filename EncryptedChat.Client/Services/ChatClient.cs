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

        public static Result<T> Ok(T value) => new() { Success = true, Value = value };
        public static Result<T> Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    // GET api/Message/team/{teamId}
    public async Task<Result<List<MessageDTOPublic>>> GetMessagesByTeamAsync(Guid teamId)
    {
        var res = await _http.GetAsync($"api/Message/team/{teamId}");
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            return Result<List<MessageDTOPublic>>.Fail($"Failed to load messages ({res.StatusCode}).");
        }

        var msgs = JsonSerializer.Deserialize<List<MessageDTOPublic>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        return Result<List<MessageDTOPublic>>.Ok(msgs);
    }

    // Optional: REST send via POST api/Message (even though SignalR will mainly be used)
    private class MessageDTO
    {
        public string Text { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public Guid Team { get; set; }
    }

    public async Task<Result<MessageDTOPublic>> SendMessageRestAsync(string senderId, Guid teamId, string text)
    {
        var dto = new MessageDTO { Text = text, Sender = senderId, Team = teamId };
        var json = JsonSerializer.Serialize(dto);

        var req = new HttpRequestMessage(HttpMethod.Post, "api/Message")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

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