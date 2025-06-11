namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

public class UserDTOSecret
{
    [Required]
    public string? Email { get; set; }

    [Required]
    public string? Secret { get; set; }
}

