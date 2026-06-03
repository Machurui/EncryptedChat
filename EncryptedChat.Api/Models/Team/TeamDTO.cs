namespace EncryptedChat.Models;

using System.ComponentModel.DataAnnotations;

[NoAdminMemberOverlap]
public class TeamDTO
{
    [Required]
    [MinLength(1, ErrorMessage = "Au moins un admin requis")]
    public ICollection<string> Admins { get; set; } = [];

    public ICollection<string>? Members { get; set; } = [];

    [Required]
    [MaxLength(100)]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Glyph { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    [MaxLength(10)]
    public string? MessageLifetime { get; set; }

    // Base64 ECIES-wrapped Team.Secret for the creator. Generated client-side at
    // team creation. Server stores it in TeamKeyShare(team, creator, gen=1, this).
    // Null only for the legacy GetOrCreateDirectMessage path which v2 will
    // handle with a deferred key-share provisioning step.
    [MaxLength(256)]
    public string? InitialKeyShare { get; set; }

    // True E2E multi-member: keyed by member userId, value is the base64
    // ECIES-wrapped Team.Secret for that member. The creator wraps the same
    // secret with each member's EncryptionPublicKey client-side; the server
    // inserts one TeamKeyShare per entry atomically with the team itself,
    // so no member is ever left without keys. Required to cover every entry
    // in Admins ∪ Members (creator included). Server rejects with 400 if a
    // member appears without a corresponding wrapped key.
    public Dictionary<string, string>? MemberKeyShares { get; set; }
}
