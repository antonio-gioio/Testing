using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class Shipment : OrganizationScopedEntity
{
    public string Reference { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Draft;
    public string? CarrierId { get; set; }
    public string? CarrierName { get; set; }
    public string? ServiceName { get; set; }
    public string? OriginPort { get; set; }
    public string? OriginPortCode { get; set; }
    public string? DestinationPort { get; set; }
    public string? DestinationPortCode { get; set; }
    public DateTime? EstimatedDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public string? VoyageNumber { get; set; }
    public Guid? VesselId { get; set; }
    public Vessel? Vessel { get; set; }
    public string? Incoterms { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsDelayDetected { get; set; } = false;
    public int? DelayDays { get; set; }
    public string? CreatedByUserId { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = new();

    public ICollection<Container> Containers { get; set; } = new List<Container>();
    public ICollection<BillOfLading> BillsOfLading { get; set; } = new List<BillOfLading>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
