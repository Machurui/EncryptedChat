namespace EncryptedChat.Models;
using System.ComponentModel.DataAnnotations;

public class UserUpdateDTO
{
    [MinLength(2)]
    [MaxLength(100)]
    public string? Name { get; set; }

    [MinLength(3)]
    [MaxLength(32)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Handle can only contain letters, numbers, and underscores")]
    public string? Handle { get; set; }

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; set; }

    // Format validation is performed in UserService.UpdateAsync against
    // CssColorRegex which accepts #RRGGBB, rgb/rgba, hsl/hsla, oklch, oklab.
    // No DataAnnotation regex here — a stricter regex would reject oklch
    // colors before the controller body runs.
    public string? NameColor { get; set; }

    [MaxLength(500)]
    [Url]
    public string? ProfileImageUrl { get; set; }

    [RegularExpression(@"^(online|away|busy|invisible)$", ErrorMessage = "Invalid status")]
    public string? Status { get; set; }

    [MaxLength(100)]
    public string? StatusMessage { get; set; }

    [RegularExpression(@"^(dark|light|auto)$", ErrorMessage = "Invalid theme")]
    public string? Theme { get; set; }

    public bool? ReadReceipts { get; set; }
    public bool? TypingIndicators { get; set; }

    [RegularExpression(@"^(all|mentions|none)$", ErrorMessage = "Invalid notification preference")]
    public string? NotificationPreference { get; set; }
}
