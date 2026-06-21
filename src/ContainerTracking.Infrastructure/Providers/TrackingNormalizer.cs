using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

public class TrackingNormalizer : ITrackingNormalizer
{
    private readonly ILogger<TrackingNormalizer> _logger;

    public TrackingNormalizer(ILogger<TrackingNormalizer> logger)
    {
        _logger = logger;
    }

    public NormalizedTrackingEvent Normalize(RawTrackingData rawData, TrackingProviderType providerType)
    {
        return providerType switch
        {
            TrackingProviderType.Ais => NormalizeAis(rawData),
            TrackingProviderType.Carrier => NormalizeCarrier(rawData),
            TrackingProviderType.PortApi => NormalizePortEvent(rawData),
            TrackingProviderType.Manual => NormalizeManual(rawData),
            TrackingProviderType.CsvImport => NormalizeCsvImport(rawData),
            _ => NormalizeGeneric(rawData, providerType)
        };
    }

    public IEnumerable<NormalizedTrackingEvent> NormalizeBatch(
        IEnumerable<RawTrackingData> rawDataItems, TrackingProviderType providerType)
    {
        return rawDataItems
            .Select(raw =>
            {
                try { return Normalize(raw, providerType); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to normalize event from {Provider}", raw.SourceProvider);
                    return null;
                }
            })
            .Where(e => e != null)
            .Cast<NormalizedTrackingEvent>();
    }

    private NormalizedTrackingEvent NormalizeAis(RawTrackingData raw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(raw.RawJson);
        var root = doc.RootElement;

        return new NormalizedTrackingEvent
        {
            ProviderCode = raw.SourceProvider,
            ProviderType = TrackingProviderType.Ais,
            EventType = TrackingEventType.AisPositionUpdate,
            EventTime = raw.ReceivedAt,
            Description = "AIS position update",
            Latitude = root.TryGetProperty("lat", out var lat) ? lat.GetDouble() : null,
            Longitude = root.TryGetProperty("lon", out var lon) ? lon.GetDouble() : null,
            SpeedKnots = root.TryGetProperty("speed", out var spd) ? spd.GetDouble() : null,
            Heading = root.TryGetProperty("heading", out var hdg) ? hdg.GetDouble() : null,
            VesselImo = root.TryGetProperty("imo", out var imo) ? imo.GetString() : null,
            VesselName = root.TryGetProperty("name", out var name) ? name.GetString() : null,
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private NormalizedTrackingEvent NormalizeCarrier(RawTrackingData raw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(raw.RawJson);
        var root = doc.RootElement;

        return new NormalizedTrackingEvent
        {
            ProviderCode = raw.SourceProvider,
            ProviderType = TrackingProviderType.Carrier,
            EventType = MapCarrierEventType(root.TryGetProperty("eventCode", out var ec) ? ec.GetString() : null),
            EventTime = root.TryGetProperty("eventTime", out var et) ? DateTime.Parse(et.GetString()!) : raw.ReceivedAt,
            Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            Location = root.TryGetProperty("locationName", out var ln) ? ln.GetString() : null,
            LocationCode = root.TryGetProperty("locationCode", out var lc) ? lc.GetString() : null,
            ContainerNumber = root.TryGetProperty("containerNumber", out var cn) ? cn.GetString() : null,
            VesselName = root.TryGetProperty("vesselName", out var vn) ? vn.GetString() : null,
            VoyageNumber = root.TryGetProperty("voyageNumber", out var vy) ? vy.GetString() : null,
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private NormalizedTrackingEvent NormalizePortEvent(RawTrackingData raw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(raw.RawJson);
        var root = doc.RootElement;

        return new NormalizedTrackingEvent
        {
            ProviderCode = raw.SourceProvider,
            ProviderType = TrackingProviderType.PortApi,
            EventType = TrackingEventType.PortCallEvent,
            EventTime = root.TryGetProperty("timestamp", out var ts) ? DateTime.Parse(ts.GetString()!) : raw.ReceivedAt,
            Description = root.TryGetProperty("eventDescription", out var d) ? d.GetString() ?? "Port call event" : "Port call event",
            LocationCode = root.TryGetProperty("portCode", out var pc) ? pc.GetString() : null,
            VesselImo = root.TryGetProperty("imo", out var imo) ? imo.GetString() : null,
            VesselName = root.TryGetProperty("vesselName", out var vn) ? vn.GetString() : null,
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private NormalizedTrackingEvent NormalizeManual(RawTrackingData raw)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(raw.RawJson);
        var root = doc.RootElement;

        return new NormalizedTrackingEvent
        {
            ProviderCode = "MANUAL",
            ProviderType = TrackingProviderType.Manual,
            EventType = TrackingEventType.ManualUpdate,
            EventTime = root.TryGetProperty("eventTime", out var et) ? DateTime.Parse(et.GetString()!) : raw.ReceivedAt,
            Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
            Location = root.TryGetProperty("location", out var loc) ? loc.GetString() : null,
            ContainerNumber = root.TryGetProperty("containerNumber", out var cn) ? cn.GetString() : null,
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private NormalizedTrackingEvent NormalizeCsvImport(RawTrackingData raw)
    {
        return new NormalizedTrackingEvent
        {
            ProviderCode = "CSV_IMPORT",
            ProviderType = TrackingProviderType.CsvImport,
            EventType = TrackingEventType.ImportedEvent,
            EventTime = raw.ReceivedAt,
            Description = "Imported via CSV",
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private NormalizedTrackingEvent NormalizeGeneric(RawTrackingData raw, TrackingProviderType type)
    {
        return new NormalizedTrackingEvent
        {
            ProviderCode = raw.SourceProvider,
            ProviderType = type,
            EventType = TrackingEventType.PortCallEvent,
            EventTime = raw.ReceivedAt,
            Description = "Tracking event",
            RawData = new Dictionary<string, string> { ["raw"] = raw.RawJson }
        };
    }

    private TrackingEventType MapCarrierEventType(string? code) => code?.ToUpperInvariant() switch
    {
        "GATE-IN" or "GATE_IN" or "GI" => TrackingEventType.ContainerGateIn,
        "LOAD" or "LOADED" => TrackingEventType.ContainerLoaded,
        "DEPART" or "DEPARTURE" or "VD" => TrackingEventType.VesselDeparted,
        "ARRIVE" or "ARRIVAL" or "VA" => TrackingEventType.VesselArrived,
        "DISC" or "DISCHARGE" or "CD" => TrackingEventType.ContainerDischarged,
        "GATE-OUT" or "GATE_OUT" or "GO" => TrackingEventType.ContainerGateOut,
        "DELIVER" or "DELIVERY" => TrackingEventType.ContainerDelivered,
        "TRANSSHIP" or "TS" => TrackingEventType.TransshipmentArrived,
        "ETA" => TrackingEventType.EtaUpdated,
        _ => TrackingEventType.PortCallEvent
    };
}
