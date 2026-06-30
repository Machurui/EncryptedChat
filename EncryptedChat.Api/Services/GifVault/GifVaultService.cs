using EncryptedChat.Data;
using EncryptedChat.Models;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public sealed class GifVaultService(EncryptedChatContext db) : IGifVaultService
{
    private readonly EncryptedChatContext _db = db;

    public async Task<GifVaultReadDTO?> GetAsync(string userId, CancellationToken ct)
    {
        UserGifVault? v = await _db.UserGifVaults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        return v is null ? null : new GifVaultReadDTO(v.WrappedKey, v.Iv, v.Blob, v.Revision);
    }

    public async Task<GifVaultUpsertResult> UpsertAsync(string userId, GifVaultWriteDTO dto, CancellationToken ct)
    {
        UserGifVault? existing = await _db.UserGifVaults.FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (existing is null)
        {
            if (dto.ExpectedRevision != 0)
                return new GifVaultUpsertResult(GifVaultUpsertKind.Conflict, 0);

            _db.UserGifVaults.Add(new UserGifVault
            {
                UserId = userId,
                WrappedKey = dto.WrappedKey,
                Iv = dto.Iv,
                Blob = dto.Blob,
                Revision = 1,
                UpdatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            return new GifVaultUpsertResult(GifVaultUpsertKind.Ok, 1);
        }

        if (existing.Revision != dto.ExpectedRevision)
            return new GifVaultUpsertResult(GifVaultUpsertKind.Conflict, existing.Revision);

        existing.WrappedKey = dto.WrappedKey;
        existing.Iv = dto.Iv;
        existing.Blob = dto.Blob;
        existing.Revision += 1;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return new GifVaultUpsertResult(GifVaultUpsertKind.Ok, existing.Revision);
    }
}
