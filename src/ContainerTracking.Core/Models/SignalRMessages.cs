using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Models;

public class ContainerUpdateMessage
{
    public Guid ContainerId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ContainerNumber { get; set; } = string.Empty;
    public ContainerStatus Status { get; set; }
    public string? CurrentLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? EtaDestination { get; set; }
    public string EventDescription { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
}

public class AlertNotificationMessage
{
    public Guid AlertId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime TriggeredAt { get; set; }
}

public class VesselPositionMessage
{
    public string Imo { get; set; } = string.Empty;
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DashboardRefreshMessage
{
    public Guid OrganizationId { get; set; }
    public string RefreshType { get; set; } = "partial";
    public List<string> WidgetTypes { get; set; } = new();
}
