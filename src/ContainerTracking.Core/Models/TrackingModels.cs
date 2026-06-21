using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Models;

public class NormalizedTrackingEvent
{
    public string? ExternalId { get; set; }
    public TrackingEventType EventType { get; set; }
    public TrackingProviderType ProviderType { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ContainerNumber { get; set; }
    public string? BolNumber { get; set; }
    public string? Location { get; set; }
    public string? LocationCode { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? VesselName { get; set; }
    public string? VesselImo { get; set; }
    public string? VoyageNumber { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Heading { get; set; }
    public ContainerStatus? StatusAfter { get; set; }
    public Dictionary<string, string> RawData { get; set; } = new();
}

public class RawTrackingData
{
    public string SourceProvider { get; set; } = string.Empty;
    public string RawJson { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class VesselPositionResult
{
    public string Imo { get; set; } = string.Empty;
    public string? Mmsi { get; set; }
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKnots { get; set; }
    public double? Heading { get; set; }
    public string? Destination { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    public string? NavigationStatus { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ProviderHealthResult
{
    public bool IsHealthy { get; set; }
    public string? Message { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
