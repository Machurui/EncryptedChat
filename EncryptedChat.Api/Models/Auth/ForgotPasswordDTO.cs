namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class ForgotPasswordDTO
{
    [Required]
    public string Email { get; set; } = string.Empty;
}