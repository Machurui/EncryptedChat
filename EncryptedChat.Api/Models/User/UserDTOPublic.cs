namespace EncryptedChat.Models;

// Affichage vers le client
public class UserDTOPublic
{
    public string? Id { get; set; } = string.Empty;
    public string? FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; } = string.Empty;

    public string? Email { get; set; } = string.Empty;

    public int Level { get; set; } = 0;
}