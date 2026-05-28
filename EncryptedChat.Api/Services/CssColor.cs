using System.Text.RegularExpressions;

namespace EncryptedChat.Services;

public static class CssColor
{
    public static readonly Regex Regex =
        new(@"^(#[0-9A-Fa-f]{6}|rgba?\([^)]{1,80}\)|hsla?\([^)]{1,80}\)|okl(ch|ab)\([^)]{1,80}\))$",
            RegexOptions.Compiled);

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value);
}
