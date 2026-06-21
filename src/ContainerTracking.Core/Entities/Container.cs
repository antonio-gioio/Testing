using ContainerTracking.Core.Enums;
using NetTopologySuite.Geometries;

namespace ContainerTracking.Core.Entities;

public class Container : OrganizationScopedEntity
{
    public string ContainerNumber { get; set; } = string.Empty;
    public ContainerStatus Status { get; set; } = ContainerStatus.Unknown;
    public string? SizeType { get; set; }
    public string? IsoType { get; set; }
    public string? CargoDescription { get; set; }
    public decimal? WeightKg { get; set; }
    public bool IsHazmat { get; set; } = false;
    public string? HazmatClass { get; set; }
    public bool IsReeferRequired { get; set; } = false;
    public double? TemperatureSetPoint { get; set; }
    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public Guid? BillOfLadingId { get; set; }
    public BillOfLading? BillOfLading { get; set; }
    public string? CurrentLocation { get; set; }
    public string? CurrentPortCode { get; set; }
    public Point? CurrentPosition { get; set; }
    public DateTime? LastEventAt { get; set; }
    public DateTime? EtaDestination { get; set; }
    public DateTime? ActualArrival { get; set; }
    public bool IsTrackingActive { get; set; } = true;
    public DateTime? LastPolledAt { get; set; }
    public string? LastProviderResponse { get; set; }
    public string? PublicTrackingToken { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();

    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
