namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class LoginDTO
{
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}