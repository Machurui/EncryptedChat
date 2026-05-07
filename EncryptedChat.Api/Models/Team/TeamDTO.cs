namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

[NoAdminMemberOverlap]
public class TeamDTO
{
    [Required]
    public ICollection<string>? Admins { get; set; } = [];

    public ICollection<string>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    public string? Name { get; set; } = string.Empty;
}
