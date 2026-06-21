using NetTopologySuite.Geometries;

namespace ContainerTracking.Core.Entities;

public class Vessel : BaseEntity
{
    public string Imo { get; set; } = string.Empty;
    public string? Mmsi { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CallSign { get; set; }
    public string? Flag { get; set; }
    public string? Type { get; set; }
    public string? ShippingLine { get; set; }
    public double? Length { get; set; }
    public double? Beam { get; set; }
    public double? Draught { get; set; }
    public int? YearBuilt { get; set; }
    public Point? CurrentPosition { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Heading { get; set; }
    public string? NavigationStatus { get; set; }
    public string? Destination { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public DateTime? LastPositionUpdate { get; set; }
    public string? LastKnownPort { get; set; }

    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
