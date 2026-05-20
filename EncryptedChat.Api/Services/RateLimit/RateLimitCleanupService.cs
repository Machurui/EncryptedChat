namespace EncryptedChat.Services;

public class RateLimitCleanupService(IRateLimitService rateLimitService) : BackgroundService
{
    private readonly IRateLimitService _rateLimitService = rateLimitService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _rateLimitService.CleanupStaleEntries(TimeSpan.FromMinutes(10));
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
