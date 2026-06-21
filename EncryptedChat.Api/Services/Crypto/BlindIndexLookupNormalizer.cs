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

    public string? NormalizeName(string? name) =>
        name is null ? null : _blindIndex.Compute(name.Trim().ToLowerInvariant(), Purpose);

    public string? NormalizeEmail(string? email) =>
        email is null ? null : _blindIndex.Compute(email.Trim().ToLowerInvariant(), Purpose);
}
