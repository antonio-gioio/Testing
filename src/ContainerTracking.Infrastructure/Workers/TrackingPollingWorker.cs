using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Workers;

/// <summary>
/// Background worker that polls tracking providers for container updates.
/// Respects tier-based polling intervals per organization.
/// After storing events it publishes updates to Redis/SignalR.
/// </summary>
public class TrackingPollingWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TrackingPollingWorker> _logger;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMinutes(5);

    public TrackingPollingWorker(IServiceProvider services, ILogger<TrackingPollingWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TrackingPollingWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllOrganizationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TrackingPollingWorker encountered an error");
            }

            await Task.Delay(DefaultPollInterval, stoppingToken);
        }
    }

    private async Task PollAllOrganizationsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tierService = scope.ServiceProvider.GetRequiredService<ITierEnforcementService>();
        var providers = scope.ServiceProvider.GetServices<ITrackingProvider>().ToList();
        var signalRPublisher = scope.ServiceProvider.GetRequiredService<ISignalRPublisher>();

        var organizations = await db.Organizations
            .Where(o => o.IsActive && !o.IsDeleted)
            .Select(o => o.Id)
            .ToListAsync(ct);

        foreach (var orgId in organizations)
        {
            try
            {
                await PollOrganizationContainersAsync(orgId, db, providers, tierService, signalRPublisher, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll containers for organization {OrgId}", orgId);
            }
        }
    }

    private async Task PollOrganizationContainersAsync(
        Guid orgId, AppDbContext db, List<ITrackingProvider> providers,
        ITierEnforcementService tierService, ISignalRPublisher publisher,
        CancellationToken ct)
    {
        var limits = await tierService.GetLimitsAsync(orgId, ct);
        var pollIntervalMinutes = limits.UpdateIntervalMinutes;

        var cutoff = DateTime.UtcNow.AddMinutes(-pollIntervalMinutes);
        var containers = await db.Containers
            .Where(c => c.OrganizationId == orgId
                     && c.IsTrackingActive
                     && !c.IsDeleted
                     && (c.LastPolledAt == null || c.LastPolledAt < cutoff))
            .Take(50)
            .ToListAsync(ct);

        if (!containers.Any()) return;

        var orgCredentials = await db.ProviderCredentials
            .Include(pc => pc.TrackingProvider)
            .Where(pc => pc.OrganizationId == orgId && pc.IsEnabled)
            .ToListAsync(ct);

        foreach (var container in containers)
        {
            await PollContainerAsync(container, orgId, providers, orgCredentials, db, publisher, ct);
            container.LastPolledAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task PollContainerAsync(
        Container container, Guid orgId,
        List<ITrackingProvider> providers,
        List<ProviderCredential> orgCredentials,
        AppDbContext db, ISignalRPublisher publisher,
        CancellationToken ct)
    {
        foreach (var provider in providers.Where(p => p.IsAvailable && p is not IAisProvider))
        {
            try
            {
                var credentials = GetCredentialsForProvider(orgCredentials, provider.ProviderCode);
                if (credentials == null && provider is ICarrierTrackingProvider) continue;

                var events = await provider.GetContainerEventsAsync(
                    container.ContainerNumber, credentials, ct);

                await ProcessAndStoreEventsAsync(events, container, orgId, db, publisher, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed for container {Container}",
                    provider.ProviderCode, container.ContainerNumber);
            }
        }
    }

    private async Task ProcessAndStoreEventsAsync(
        IEnumerable<NormalizedTrackingEvent> events,
        Container container, Guid orgId, AppDbContext db,
        ISignalRPublisher publisher, CancellationToken ct)
    {
        var newEvents = new List<TrackingEvent>();

        foreach (var evt in events)
        {
            var alreadyExists = await db.TrackingEvents.AnyAsync(e =>
                e.ExternalEventId == evt.ExternalId &&
                e.ProviderType == evt.ProviderType &&
                e.ContainerId == container.Id, ct);

            if (alreadyExists) continue;

            var trackingEvent = new TrackingEvent
            {
                OrganizationId = orgId,
                ContainerId = container.Id,
                ShipmentId = container.ShipmentId,
                EventType = evt.EventType,
                ProviderType = evt.ProviderType,
                ProviderName = evt.ProviderCode,
                ExternalEventId = evt.ExternalId,
                EventTime = evt.EventTime,
                Description = evt.Description,
                Location = evt.Location,
                LocationCode = evt.LocationCode,
                CountryCode = evt.CountryCode,
                VesselName = evt.VesselName,
                VesselImo = evt.VesselImo,
                VoyageNumber = evt.VoyageNumber,
                ContainerStatusAfter = evt.StatusAfter,
                RawData = evt.RawData,
                ReceivedAt = DateTime.UtcNow
            };

            if (evt.Latitude.HasValue && evt.Longitude.HasValue)
            {
                trackingEvent.GeoPosition = new NetTopologySuite.Geometries.Point(
                    evt.Longitude.Value, evt.Latitude.Value) { SRID = 4326 };
            }

            newEvents.Add(trackingEvent);
            db.TrackingEvents.Add(trackingEvent);

            if (evt.StatusAfter.HasValue)
            {
                container.Status = evt.StatusAfter.Value;
                container.LastEventAt = evt.EventTime;
                if (evt.Latitude.HasValue && evt.Longitude.HasValue)
                {
                    container.CurrentPosition = new NetTopologySuite.Geometries.Point(
                        evt.Longitude.Value, evt.Latitude.Value) { SRID = 4326 };
                }
                container.CurrentLocation = evt.Location;
                container.CurrentPortCode = evt.LocationCode;
            }
        }

        if (newEvents.Any())
        {
            var latestEvent = newEvents.OrderByDescending(e => e.EventTime).First();
            await publisher.PublishContainerUpdateAsync(new ContainerUpdateMessage
            {
                ContainerId = container.Id,
                OrganizationId = orgId,
                ContainerNumber = container.ContainerNumber,
                Status = container.Status,
                CurrentLocation = container.CurrentLocation,
                Latitude = latestEvent.GeoPosition?.Y,
                Longitude = latestEvent.GeoPosition?.X,
                EtaDestination = container.EtaDestination,
                EventDescription = latestEvent.Description,
                EventTime = latestEvent.EventTime,
                ProviderCode = latestEvent.ProviderName ?? ""
            }, ct);
        }
    }

    private Dictionary<string, string>? GetCredentialsForProvider(
        List<ProviderCredential> credentials, string providerCode)
    {
        var cred = credentials.FirstOrDefault(c => c.TrackingProvider.Code == providerCode);
        if (cred == null) return null;

        return new Dictionary<string, string>
        {
            ["api_key"] = cred.EncryptedApiKey
        };
    }
}

public interface ISignalRPublisher
{
    Task PublishContainerUpdateAsync(ContainerUpdateMessage message, CancellationToken ct = default);
    Task PublishAlertAsync(AlertNotificationMessage message, CancellationToken ct = default);
    Task PublishVesselPositionAsync(VesselPositionMessage message, CancellationToken ct = default);
}
