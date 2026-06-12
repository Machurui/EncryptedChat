using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IGifService
{
    Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, bool stickers, CancellationToken ct);
    Task<List<GifResultDTO>> TrendingAsync(int limit, int offset, bool stickers, CancellationToken ct);
    Task<GifResultDTO?> RandomAsync(string? tag, bool stickers, CancellationToken ct);
    Task<List<GifCategoryDTO>> CategoriesAsync(CancellationToken ct);
}
