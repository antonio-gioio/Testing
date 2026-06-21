using ContainerTracking.Infrastructure.Data;
using ContainerTracking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Workers;

public class AlertProcessingWorker(IServiceScopeFactory scopeFactory, ILogger<AlertProcessingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Alert processing worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alert processing worker error");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<AlertProcessingService>();

        var unprocessed = await db.TrackingEvents
            .Include(e => e.Container)
            .Include(e => e.Shipment)
            .Where(e => !e.IsProcessed && !e.IsDeleted)
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var evt in unprocessed)
        {
            await processor.ProcessEventAsync(evt, evt.Container, evt.Shipment);
            evt.IsProcessed = true;
        }

        if (unprocessed.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogDebug("Processed {Count} tracking events for alerts", unprocessed.Count);
        }
    }
}
