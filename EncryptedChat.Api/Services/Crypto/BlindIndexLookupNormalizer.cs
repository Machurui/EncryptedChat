using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Services;

// Replaces Identity's default ILookupNormalizer so that "normalizing" an email/username
// produces a deterministic blind index. NormalizedEmail/NormalizedUserName then store the
// blind index, so FindByEmail/FindByName/uniqueness keep working while the Email/UserName
// columns are encrypted at rest. No custom UserStore needed.
public sealed class BlindIndexLookupNormalizer(IBlindIndex blindIndex) : ILookupNormalizer
{
    private const string Purpose = "identity";
    private readonly IBlindIndex _blindIndex = blindIndex;

    // This normalizer is shared by UserManager AND RoleManager. Usernames are emails (they
    // contain '@') and MUST be blind-indexed so the email never appears in NormalizedUserName.
    // Role names are a fixed vocabulary (User/Admin/App, no '@') and keep the standard
    // upper-invariant normalization, so the HasData-seeded AspNetRoles rows
    // (NormalizedName = "USER"/"ADMIN"/"APP") still resolve.
    public string? NormalizeName(string? name)
    {
        if (name is null) return null;
        return name.Contains('@')
            ? _blindIndex.Compute(name.Trim().ToLowerInvariant(), Purpose)
            : name.ToUpperInvariant();
    }

    public string? NormalizeEmail(string? email) =>
        email is null ? null : _blindIndex.Compute(email.Trim().ToLowerInvariant(), Purpose);
}
