using Microsoft.AspNetCore.Identity;

namespace EncryptedChat.Services;

public sealed class BlindIndexLookupNormalizer(IBlindIndex blindIndex) : ILookupNormalizer
{
    private const string Purpose = "identity";
    private readonly IBlindIndex _blindIndex = blindIndex;

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
