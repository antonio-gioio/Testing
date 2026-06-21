using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace ContainerTracking.Infrastructure.Workers;

public class VesselPositionWorker(IServiceScopeFactory scopeFactory, ILogger<VesselPositionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Vessel position worker started (interval: {Interval})", Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollVesselPositionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Vessel position polling error");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task PollVesselPositionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ais = scope.ServiceProvider.GetRequiredService<IAisProvider>();
        var publisher = scope.ServiceProvider.GetRequiredService<ISignalRPublisher>();

        // Only poll vessels that have active shipments or haven't been updated recently
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var vessels = await db.Vessels
            .Where(v => !v.IsDeleted && !string.IsNullOrEmpty(v.Mmsi)
                        && (v.LastPositionUpdate == null || v.LastPositionUpdate < cutoff))
            .OrderBy(v => v.LastPositionUpdate)
            .Take(50)
            .ToListAsync(ct);

        if (vessels.Count == 0) return;

        int updated = 0;
        foreach (var vessel in vessels)
        {
            try
            {
                var result = await ais.GetVesselPositionByMmsiAsync(vessel.Mmsi!, ct);
                if (result == null) continue;

                vessel.CurrentPosition = new Point(result.Longitude, result.Latitude) { SRID = 4326 };
                vessel.SpeedKnots = result.SpeedKnots;
                vessel.Heading = result.Heading;
                vessel.NavigationStatus = result.NavigationStatus;
                vessel.Destination = result.Destination;
                vessel.EstimatedArrival = result.EstimatedArrival;
                vessel.LastPositionUpdate = result.Timestamp;
                vessel.UpdatedAt = DateTime.UtcNow;
                updated++;

                await publisher.PublishVesselPositionAsync(new Core.Models.VesselPositionMessage
                {
                    Imo = vessel.Imo,
                    Name = vessel.Name,
                    Latitude = result.Latitude,
                    Longitude = result.Longitude,
                    SpeedKnots = result.SpeedKnots,
                    Heading = result.Heading,
                    Timestamp = result.Timestamp
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update position for vessel {Mmsi}", vessel.Mmsi);
            }
        }

        if (updated > 0) await db.SaveChangesAsync(ct);
        if (updated > 0) logger.LogInformation("Updated positions for {Count}/{Total} vessels", updated, vessels.Count);
    }
}
