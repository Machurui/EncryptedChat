using EncryptedChat.Models;

namespace EncryptedChat.Services;

public enum GifVaultUpsertKind { Ok, Conflict }

public sealed record GifVaultUpsertResult(GifVaultUpsertKind Kind, int Revision);

public interface IGifVaultService
{
    Task<GifVaultReadDTO?> GetAsync(string userId, CancellationToken ct);
    Task<GifVaultUpsertResult> UpsertAsync(string userId, GifVaultWriteDTO dto, CancellationToken ct);
}
