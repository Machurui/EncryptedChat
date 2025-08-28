namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

// Utilisé lors de création et MAJ
public class TeamDTO
{
    [Required]
    public ICollection<string>? AdminIds { get; set; } = [];

    public ICollection<string>? MemberIds { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Password { get; set; } = string.Empty;
}
