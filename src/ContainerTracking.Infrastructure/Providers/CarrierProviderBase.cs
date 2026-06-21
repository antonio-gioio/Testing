using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

/// <summary>
/// Base class for carrier-specific tracking adapters.
/// Concrete implementations connect to individual shipping line APIs
/// (Maersk, MSC, CMA CGM, etc.) each with their own auth/format.
/// </summary>
public abstract class CarrierProviderBase : ICarrierTrackingProvider
{
    protected readonly HttpClient Http;
    protected readonly ILogger Logger;

    protected CarrierProviderBase(HttpClient http, ILogger logger)
    {
        Http = http;
        Logger = logger;
    }

    public abstract string ProviderCode { get; }
    public abstract string ProviderName { get; }
    public abstract string CarrierCode { get; }
    public TrackingProviderType ProviderType => TrackingProviderType.Carrier;
    public virtual bool IsAvailable => true;

    public abstract Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default);

    public abstract Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default);

    public virtual async Task<IEnumerable<NormalizedTrackingEvent>> GetShipmentEventsAsync(
        string shipmentReference,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        return await GetContainerEventsAsync(shipmentReference, credentials, ct);
    }

    public abstract Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default);

    protected string GetCredential(Dictionary<string, string>? credentials, string key)
    {
        if (credentials != null && credentials.TryGetValue(key, out var value))
            return value;
        throw new InvalidOperationException($"Missing credential '{key}' for provider {ProviderCode}");
    }
}

/// <summary>
/// Placeholder adapter for Maersk Line tracking API.
/// Commercial API key required from developer.maersk.com
/// </summary>
public class MaerskCarrierProvider : CarrierProviderBase
{
    public override string ProviderCode => "MAERSK";
    public override string ProviderName => "Maersk Line";
    public override string CarrierCode => "MAEU";

    public MaerskCarrierProvider(HttpClient http, ILogger<MaerskCarrierProvider> logger)
        : base(http, logger) { }

    public override async Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber, Dictionary<string, string>? credentials = null, CancellationToken ct = default)
    {
        try
        {
            var apiKey = GetCredential(credentials, "api_key");
            Http.DefaultRequestHeaders.Remove("Consumer-Key");
            Http.DefaultRequestHeaders.Add("Consumer-Key", apiKey);

            var response = await Http.GetAsync(
                $"https://api.maersk.com/track/v1/containers/{containerNumber}", ct);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Maersk API returned {Status} for container {Container}", response.StatusCode, containerNumber);
                return Enumerable.Empty<NormalizedTrackingEvent>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return NormalizeMaerskResponse(json, containerNumber);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Maersk tracking failed for {Container}", containerNumber);
            return Enumerable.Empty<NormalizedTrackingEvent>();
        }
    }

    public override async Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber, Dictionary<string, string>? credentials = null, CancellationToken ct = default)
    {
        // Maersk BOL tracking uses same endpoint with bol filter
        return await GetContainerEventsAsync(bolNumber, credentials, ct);
    }

    public override async Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await Http.GetAsync("https://api.maersk.com/status", ct);
            sw.Stop();
            return new ProviderHealthResult { IsHealthy = response.IsSuccessStatusCode, ResponseTime = sw.Elapsed };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProviderHealthResult { IsHealthy = false, Message = ex.Message, ResponseTime = sw.Elapsed };
        }
    }

    private IEnumerable<NormalizedTrackingEvent> NormalizeMaerskResponse(string json, string containerNumber)
    {
        // Parse Maersk-specific JSON format and normalize
        var events = new List<NormalizedTrackingEvent>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("containers", out var containers)) return events;

            foreach (var container in containers.EnumerateArray())
            {
                if (!container.TryGetProperty("events", out var evts)) continue;
                foreach (var evt in evts.EnumerateArray())
                {
                    events.Add(new NormalizedTrackingEvent
                    {
                        ProviderCode = ProviderCode,
                        ProviderType = ProviderType,
                        ContainerNumber = containerNumber,
                        EventType = MapEventTypeCode(
                            evt.TryGetProperty("eventTypeCode", out var tc) ? tc.GetString() : ""),
                        Description = evt.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        EventTime = evt.TryGetProperty("eventDateTime", out var dt)
                            ? DateTime.Parse(dt.GetString()!) : DateTime.UtcNow,
                        Location = evt.TryGetProperty("location", out var loc)
                            ? loc.TryGetProperty("name", out var ln) ? ln.GetString() : null : null,
                        LocationCode = evt.TryGetProperty("location", out var lc2)
                            ? lc2.TryGetProperty("unLocode", out var ulo) ? ulo.GetString() : null : null,
                        VesselName = evt.TryGetProperty("vessel", out var ves)
                            ? ves.TryGetProperty("name", out var vn) ? vn.GetString() : null : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to parse Maersk response");
        }
        return events;
    }

    private TrackingEventType MapEventTypeCode(string? code) => code switch
    {
        "GATE-IN" => TrackingEventType.ContainerGateIn,
        "LOAD" => TrackingEventType.ContainerLoaded,
        "DEPART" => TrackingEventType.VesselDeparted,
        "ARRIVE" => TrackingEventType.VesselArrived,
        "DISC" => TrackingEventType.ContainerDischarged,
        "GATE-OUT" => TrackingEventType.ContainerGateOut,
        "DELIVER" => TrackingEventType.ContainerDelivered,
        _ => TrackingEventType.PortCallEvent
    };
}

/// <summary>
/// Stub for any carrier that can be wired up with a config-driven approach.
/// Use this when a carrier provides a standard JSON tracking API.
/// </summary>
public class GenericCarrierProvider : CarrierProviderBase
{
    private readonly string _providerCode;
    private readonly string _providerName;
    private readonly string _carrierCode;
    private readonly string _trackingUrl;

    public GenericCarrierProvider(
        string providerCode, string providerName, string carrierCode, string trackingUrl,
        HttpClient http, ILogger<GenericCarrierProvider> logger)
        : base(http, logger)
    {
        _providerCode = providerCode;
        _providerName = providerName;
        _carrierCode = carrierCode;
        _trackingUrl = trackingUrl;
    }

    public override string ProviderCode => _providerCode;
    public override string ProviderName => _providerName;
    public override string CarrierCode => _carrierCode;

    public override async Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber, Dictionary<string, string>? credentials = null, CancellationToken ct = default)
    {
        Logger.LogInformation("[{Provider}] Querying container {Container}", _providerCode, containerNumber);
        return Enumerable.Empty<NormalizedTrackingEvent>();
    }

    public override async Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber, Dictionary<string, string>? credentials = null, CancellationToken ct = default)
    {
        return Enumerable.Empty<NormalizedTrackingEvent>();
    }

    public override async Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        return new ProviderHealthResult { IsHealthy = true };
    }
}
