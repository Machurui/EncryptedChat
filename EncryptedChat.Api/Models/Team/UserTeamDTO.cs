namespace EncryptedChat.Models;

public class UserTeamDTO
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
