using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Models;

namespace ContainerTracking.Core.Interfaces;

public interface ITrackingProvider
{
    string ProviderCode { get; }
    string ProviderName { get; }
    TrackingProviderType ProviderType { get; }
    bool IsAvailable { get; }

    Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default);

    Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default);

    Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default);
}

public interface IAisProvider : ITrackingProvider
{
    Task<VesselPositionResult?> GetVesselPositionByImoAsync(string imo, CancellationToken ct = default);
    Task<VesselPositionResult?> GetVesselPositionByMmsiAsync(string mmsi, CancellationToken ct = default);
    Task<IEnumerable<VesselPositionResult>> GetVesselsInBoundingBoxAsync(
        double minLat, double minLon, double maxLat, double maxLon,
        CancellationToken ct = default);
}

public interface ICarrierTrackingProvider : ITrackingProvider
{
    string CarrierCode { get; }
    Task<IEnumerable<NormalizedTrackingEvent>> GetShipmentEventsAsync(
        string shipmentReference,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default);
}

public interface IPortMilestoneProvider : ITrackingProvider
{
    Task<IEnumerable<NormalizedTrackingEvent>> GetPortCallEventsAsync(
        string portCode,
        string? vesselImo = null,
        DateTime? from = null,
        CancellationToken ct = default);
}

public interface IManualTrackingProvider : ITrackingProvider
{
    Task<NormalizedTrackingEvent> CreateManualEventAsync(
        Guid organizationId,
        Guid containerId,
        NormalizedTrackingEvent evt,
        CancellationToken ct = default);
}

public interface ITrackingNormalizer
{
    NormalizedTrackingEvent Normalize(RawTrackingData rawData, TrackingProviderType providerType);
    IEnumerable<NormalizedTrackingEvent> NormalizeBatch(IEnumerable<RawTrackingData> rawDataItems, TrackingProviderType providerType);
}
