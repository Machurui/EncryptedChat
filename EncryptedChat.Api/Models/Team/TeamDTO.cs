namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

[NoAdminMemberOverlap]
public class TeamDTO
{
    [Required]
    [MinLength(1, ErrorMessage = "Au moins un admin requis")]
    public ICollection<string> Admins { get; set; } = [];

    public ICollection<string>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;
}
