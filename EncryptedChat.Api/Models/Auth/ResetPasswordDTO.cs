namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class ResetPasswordDTO
{
    [Required]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
    
}
