using System.Net;
using System.Net.Http.Json;

namespace EncryptedChat.Client.Services;

public sealed class InviteClient(HttpClient http)
{
    private readonly HttpClient _http = http;

    public sealed record InviteCreated(string Token, DateTime ExpiresAt);
    public sealed record InviteListItem(Guid Id, string Token, DateTime CreatedAt, DateTime ExpiresAt);
    public sealed record InvitePreview(Guid TeamId, string TeamName);

    public enum JoinResult { Ok, Invalid, NoPublicKey, Error }

    public async Task<InviteCreated?> CreateAsync(Guid teamId)
    {
        HttpResponseMessage res = await _http.PostAsync($"api/Team/{teamId}/invites", null);
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<InviteCreated>() : null;
    }

    public async Task<List<InviteListItem>> ListAsync(Guid teamId)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/Team/{teamId}/invites");
        return res.IsSuccessStatusCode ? (await res.Content.ReadFromJsonAsync<List<InviteListItem>>() ?? []) : [];
    }

    public async Task<bool> RevokeAsync(Guid teamId, Guid inviteId)
        => (await _http.DeleteAsync($"api/Team/{teamId}/invites/{inviteId}")).IsSuccessStatusCode;

    public async Task<InvitePreview?> GetPreviewAsync(string token)
    {
        HttpResponseMessage res = await _http.GetAsync($"api/Team/invite/{Uri.EscapeDataString(token)}");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<InvitePreview>() : null;
    }

    // Returns (result, team). `team` is the joined team on Ok.
    public async Task<(JoinResult Result, TeamClient.TeamDTOPublic? Team)> JoinAsync(string token)
    {
        HttpResponseMessage res = await _http.PostAsync($"api/Team/invite/{Uri.EscapeDataString(token)}/join", null);
        if (res.IsSuccessStatusCode)
            return (JoinResult.Ok, await res.Content.ReadFromJsonAsync<TeamClient.TeamDTOPublic>());
        return res.StatusCode switch
        {
            HttpStatusCode.Conflict => (JoinResult.NoPublicKey, null),
            HttpStatusCode.NotFound => (JoinResult.Invalid, null),
            _ => (JoinResult.Error, null)
        };
    }
}
