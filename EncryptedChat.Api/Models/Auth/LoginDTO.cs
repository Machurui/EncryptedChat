namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class LoginDTO
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}