using EncryptedChat.Models;

namespace EncryptedChat.Services;

public interface IGifService
{
    Task<List<GifResultDTO>> SearchAsync(string query, int limit, int offset, CancellationToken ct);
    Task<List<GifResultDTO>> TrendingAsync(int limit, int offset, CancellationToken ct);
    Task<List<GifCategoryDTO>> CategoriesAsync(CancellationToken ct);
}
