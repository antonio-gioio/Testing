using ContainerTracking.Core.Enums;
using NetTopologySuite.Geometries;

namespace ContainerTracking.Core.Entities;

public class TrackingEvent : OrganizationScopedEntity
{
    public TrackingEventType EventType { get; set; }
    public TrackingProviderType ProviderType { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderId { get; set; }
    public string? ExternalEventId { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime? ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Location { get; set; }
    public string? LocationCode { get; set; }
    public string? CountryCode { get; set; }
    public Point? GeoPosition { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? VoyageNumber { get; set; }
    public string? VesselName { get; set; }
    public string? VesselImo { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Heading { get; set; }
    public ContainerStatus? ContainerStatusAfter { get; set; }

    public Guid? ContainerId { get; set; }
    public Container? Container { get; set; }
    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public Guid? BillOfLadingId { get; set; }
    public BillOfLading? BillOfLading { get; set; }
    public Guid? VesselId { get; set; }
    public Vessel? Vessel { get; set; }

    public Dictionary<string, string> RawData { get; set; } = new();
    public bool IsProcessed { get; set; } = false;
}
