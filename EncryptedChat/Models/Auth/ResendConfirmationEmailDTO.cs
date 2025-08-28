namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class ResendConfirmationEmailDTO
{
    [Required]
    public string Email { get; set; } = string.Empty;
}