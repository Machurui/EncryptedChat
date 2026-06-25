using EncryptedChat.Data;
using Microsoft.EntityFrameworkCore;

namespace EncryptedChat.Services;

// Periodically reconciles the attachment blob store against the DB: deletes .enc
// files that no Attachment row references. These orphans are left behind by
// cascade-deletes of messages/teams (the rows go, the files stay) and by the
// retention purge in MessageCleanupService (bulk ExecuteDelete). A fresh-file age
// guard avoids racing an in-flight upload whose row isn't committed yet.
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
