using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

public class ChangePasswordDTO
{
    [Required]
    [MinLength(1)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(14, ErrorMessage = "Password must be at least 14 characters")]
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

public record RecoverRequestDTO(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(12), MaxLength(12)] List<string> Words,
    [Required, MinLength(14), MaxLength(128)] string NewPassword);

public record RecoverResultDTO(string Message, IReadOnlyList<string> NewRecoveryWords);

public record EncryptionKeysDTO(
    string? SigningPublicKey,
    string? EncryptionPublicKey,
    string? EncryptedKeyBundle,
    string? KeyBundleSalt);

public record SetEncryptionKeysDTO(
    [Required] string SigningPublicKey,
    [Required] string EncryptionPublicKey,
    [Required] string EncryptedKeyBundle,
    [Required, MaxLength(64)] string KeyBundleSalt);

public record PublicKeysDTO(string SigningPublicKey, string EncryptionPublicKey);

public record TeamKeyShareDTO(
    Guid TeamId,
    int Generation,
    string WrappedKey,
    DateTime CreatedAt);

public record AddMemberKeyShareDTO(
    [Required, MaxLength(256)] string WrappedKey);

public record KeyShareEntryDTO(
    [Required] string MemberId,
    [Required, MaxLength(256)] string WrappedKey);

public record RemoveMemberDTO(
    [Required] List<KeyShareEntryDTO> NewKeyShares);
