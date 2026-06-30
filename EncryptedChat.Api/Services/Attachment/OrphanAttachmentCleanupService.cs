using EncryptedChat.Data;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

public class OrphanAttachmentCleanupService(
    IServiceProvider serviceProvider,
    ILogger<OrphanAttachmentCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    private readonly TimeSpan _minOrphanAge = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during orphan attachment cleanup");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        EncryptedChatContext context = scope.ServiceProvider.GetRequiredService<EncryptedChatContext>();
        IFileStorageService storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        HashSet<string> knownPaths = (await context.Attachments
            .AsNoTracking()
            .Select(a => a.StoragePath)
            .ToListAsync(cancellationToken)).ToHashSet();

        int removed = await storage.DeleteOrphansAsync(knownPaths, DateTime.UtcNow - _minOrphanAge, cancellationToken);
        if (removed > 0)
            logger.LogInformation("Deleted {Count} orphaned attachment blobs", removed);
    }
}
