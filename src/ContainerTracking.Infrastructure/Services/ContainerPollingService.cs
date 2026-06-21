using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using ContainerTracking.Infrastructure.Data;
using ContainerTracking.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace ContainerTracking.Infrastructure.Services;

/// <summary>
/// On-demand polling for a single container — used by the force-poll REST endpoint.
/// Mirrors the logic in TrackingPollingWorker but operates on one container at a time.
/// </summary>
public class ContainerPollingService : IContainerPollingService
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<ITrackingProvider> _providers;
    private readonly ISignalRPublisher _publisher;
    private readonly ILogger<ContainerPollingService> _logger;

    public ContainerPollingService(
        AppDbContext db,
        IEnumerable<ITrackingProvider> providers,
        ISignalRPublisher publisher,
        ILogger<ContainerPollingService> logger)
    {
        _db = db;
        _providers = providers;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> PollContainerNowAsync(Guid containerId, Guid organizationId, CancellationToken ct = default)
    {
        var container = await _db.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId && c.OrganizationId == organizationId && !c.IsDeleted, ct);

        if (container == null) return 0;

        var orgCredentials = await _db.ProviderCredentials
            .Include(pc => pc.TrackingProvider)
            .Where(pc => pc.OrganizationId == organizationId && pc.IsEnabled)
            .ToListAsync(ct);

        var totalNew = 0;
        foreach (var provider in _providers.Where(p => p.IsAvailable && p is not IAisProvider))
        {
            try
            {
                var credentials = GetCredentialsForProvider(orgCredentials, provider.ProviderCode);
                if (credentials == null && provider is ICarrierTrackingProvider) continue;

                var events = await provider.GetContainerEventsAsync(container.ContainerNumber, credentials, ct);
                var newCount = await ProcessAndStoreEventsAsync(events, container, organizationId, ct);
                totalNew += newCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed polling {Container}",
                    provider.ProviderCode, container.ContainerNumber);
            }
        }

        container.LastPolledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (totalNew > 0)
        {
            await _publisher.PublishContainerUpdateAsync(new ContainerUpdateMessage
            {
                ContainerId = container.Id,
                OrganizationId = organizationId,
                ContainerNumber = container.ContainerNumber,
                Status = container.Status,
                CurrentLocation = container.CurrentLocation,
                Latitude = container.CurrentPosition?.Y,
                Longitude = container.CurrentPosition?.X,
                EtaDestination = container.EtaDestination,
                EventDescription = $"Poll complete — {totalNew} new event(s)",
                EventTime = DateTime.UtcNow,
                ProviderCode = "poll"
            }, ct);
        }

        return totalNew;
    }

    private async Task<int> ProcessAndStoreEventsAsync(
        IEnumerable<NormalizedTrackingEvent> events,
        Container container, Guid orgId, CancellationToken ct)
    {
        int count = 0;

        foreach (var evt in events)
        {
            var alreadyExists = await _db.TrackingEvents.AnyAsync(e =>
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
                trackingEvent.GeoPosition = new Point(evt.Longitude.Value, evt.Latitude.Value) { SRID = 4326 };
            }

            _db.TrackingEvents.Add(trackingEvent);

            if (evt.StatusAfter.HasValue)
            {
                container.Status = evt.StatusAfter.Value;
                container.LastEventAt = evt.EventTime;
                container.CurrentLocation = evt.Location;
                container.CurrentPortCode = evt.LocationCode;
                if (evt.Latitude.HasValue && evt.Longitude.HasValue)
                    container.CurrentPosition = new Point(evt.Longitude.Value, evt.Latitude.Value) { SRID = 4326 };
            }

            count++;
        }

        return count;
    }

    private static Dictionary<string, string>? GetCredentialsForProvider(
        List<ProviderCredential> credentials, string providerCode)
    {
        var cred = credentials.FirstOrDefault(c => c.TrackingProvider.Code == providerCode);
        return cred == null ? null : new Dictionary<string, string> { ["api_key"] = cred.EncryptedApiKey };
    }
}
