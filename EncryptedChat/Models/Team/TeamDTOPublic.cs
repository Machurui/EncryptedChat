namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// Affichage vers le client
public class TeamDTOPublic
{
    public int Id { get; set; }

    [Required]
    public ICollection<UserDTOPublic>? Admins { get; set; } = [];

    public ICollection<UserDTOPublic>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; } = string.Empty;
}
