using System.ComponentModel.DataAnnotations;

namespace EncryptedChat.Models;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class NoAdminMemberOverlapAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not TeamDTO dto)
            return ValidationResult.Success;

        var admins = dto.Admins ?? [];
        var members = dto.Members ?? [];

        var overlap = admins.Intersect(members).ToList();

        if (overlap.Count > 0)
        {
            return new ValidationResult(
                $"A user cannot be both admin and member. Overlapping IDs: {string.Join(", ", overlap)}",
                [nameof(TeamDTO.Admins), nameof(TeamDTO.Members)]);
        }

        return ValidationResult.Success;
    }
}
