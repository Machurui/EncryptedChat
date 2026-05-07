namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

// Affichage vers le client
public class TeamDTOPublic
{
    public Guid Id { get; set; }

    public ICollection<MemberDTOPublic>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime ModifiedAt { get; set; }
}
