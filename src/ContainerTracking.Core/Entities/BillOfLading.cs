namespace ContainerTracking.Core.Entities;

public class BillOfLading : OrganizationScopedEntity
{
    public string BolNumber { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? CarrierId { get; set; }
    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public string? ConsigneeId { get; set; }
    public string? ConsigneeName { get; set; }
    public string? ShipperId { get; set; }
    public string? ShipperName { get; set; }
    public string? NotifyPartyName { get; set; }
    public string? OriginPort { get; set; }
    public string? OriginPortCode { get; set; }
    public string? DestinationPort { get; set; }
    public string? DestinationPortCode { get; set; }
    public DateTime? IssueDate { get; set; }
    public bool IsSeawaybill { get; set; } = false;
    public string? Status { get; set; }
    public string? DocumentUrl { get; set; }

    public ICollection<Container> Containers { get; set; } = new List<Container>();
    public ICollection<TrackingEvent> TrackingEvents { get; set; } = new List<TrackingEvent>();
}
