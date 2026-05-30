namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

public class ResetPasswordDTO
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(14)]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
