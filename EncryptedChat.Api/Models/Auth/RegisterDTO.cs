namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class RegisterDTO
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(32)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Handle can only contain letters, numbers, and underscores")]
    public string Handle { get; set; } = string.Empty;
}
