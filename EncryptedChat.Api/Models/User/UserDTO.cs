namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

// Utilisé lors de création et MAJ
public class UserDTO
{
    [Required]
    [MaxLength(100)]
    public string? FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string? LastName { get; set; } = string.Empty;

    [Required]
    public string? Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string? Password { get; set; } = string.Empty;
}