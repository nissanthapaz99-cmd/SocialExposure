using Microsoft.Extensions.Options;

namespace SocialExposure.Services;

public sealed class AirtableSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirtableOptions _options;
    private readonly ILogger<AirtableSyncBackgroundService> _logger;

    public AirtableSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AirtableOptions> options,
        ILogger<AirtableSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
            return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<AirtableSyncService>();
                await syncService.SyncAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The scheduled Airtable sync failed.");
            }

            var interval = TimeSpan.FromMinutes(Math.Clamp(
                _options.SyncIntervalMinutes,
                1,
                1440));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
