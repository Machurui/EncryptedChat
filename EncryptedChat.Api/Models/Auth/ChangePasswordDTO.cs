using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class ChangePasswordDTO
{
    [Required]
    [MinLength(1)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public record RecoveryPhraseDTO(
    IReadOnlyList<string> Words,
    DateTime GeneratedAt
);

public class RecoveryPhraseRequestDTO
{
    [Required]
    public string Password { get; set; } = string.Empty;
}

public record RegisterResultDTO(string Message, IReadOnlyList<string> RecoveryWords);

public record RecoverRequestDTO(string Email, List<string> Words, string NewPassword);

public record RecoverResultDTO(string Message, IReadOnlyList<string> NewRecoveryWords);
