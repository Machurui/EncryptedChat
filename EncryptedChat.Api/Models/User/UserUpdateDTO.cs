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

    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Invalid color format")]
    public string? NameColor { get; set; }

    [MaxLength(500)]
    [Url]
    public string? ProfileImageUrl { get; set; }
}
