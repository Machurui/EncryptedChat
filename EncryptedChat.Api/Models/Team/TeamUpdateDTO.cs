using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class TeamUpdateDTO
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
