namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class UserUpdateDTO
{
    [MinLength(2)]
    [MaxLength(100)]
    public string? Name { get; set; }

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }
}
