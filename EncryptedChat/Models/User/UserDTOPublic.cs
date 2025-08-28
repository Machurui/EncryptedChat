namespace EncryptedChat.Models;

// Affichage vers le client
public class UserDTOPublic
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public int Level { get; set; }
}