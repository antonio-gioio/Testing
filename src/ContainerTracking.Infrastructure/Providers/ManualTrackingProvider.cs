using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

public class ManualTrackingProvider(AppDbContext db, ILogger<ManualTrackingProvider> logger) : IManualTrackingProvider
{
    public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(Container container, ProviderCredential? credential, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<NormalizedTrackingEvent>());

    public Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(BillOfLading bol, ProviderCredential? credential, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<NormalizedTrackingEvent>());

    public async Task<NormalizedTrackingEvent> CreateManualEventAsync(
        Guid organizationId,
        Guid containerId,
        NormalizedTrackingEvent evt,
        CancellationToken ct = default)
    {
        var container = await db.Containers
            .FirstOrDefaultAsync(c => c.Id == containerId && c.OrganizationId == organizationId && !c.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"Container {containerId} not found.");

        var entity = new TrackingEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ContainerId = containerId,
            EventType = evt.EventType,
            ProviderType = TrackingProviderType.Manual,
            Description = evt.Description ?? "",
            Location = evt.Location,
            EventTime = evt.EventTime,
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var newStatus = MapEventToStatus(evt.EventType);
        if (newStatus.HasValue)
        {
            entity.ContainerStatusAfter = newStatus;
            container.Status = newStatus.Value;
            container.UpdatedAt = DateTime.UtcNow;
        }
        container.LastEventAt = entity.EventTime;

        db.TrackingEvents.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Manual event {EventType} created for container {ContainerNumber}", evt.EventType, container.ContainerNumber);
        evt.ExternalId = entity.Id.ToString();
        return evt;
    }

    private static ContainerStatus? MapEventToStatus(TrackingEventType eventType) => eventType switch
    {
        TrackingEventType.ContainerGateIn => ContainerStatus.GateIn,
        TrackingEventType.ContainerLoaded => ContainerStatus.OnVessel,
        TrackingEventType.VesselDeparted => ContainerStatus.InTransit,
        TrackingEventType.TransshipmentArrived => ContainerStatus.Transshipment,
        TrackingEventType.VesselArrived => ContainerStatus.AtDestinationPort,
        TrackingEventType.ContainerDischarged => ContainerStatus.Discharged,
        TrackingEventType.ContainerGateOut => ContainerStatus.GateOut,
        TrackingEventType.ContainerDelivered => ContainerStatus.Delivered,
        TrackingEventType.EmptyReturned => ContainerStatus.Empty,
        _ => null
    };
}
