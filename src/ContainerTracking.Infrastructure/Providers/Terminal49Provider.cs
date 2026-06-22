using System.Text.Json;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

/// <summary>
/// Terminal49 integration — provides terminal-level visibility including
/// container availability, hold status, and Last Free Day for trucker dispatch.
///
/// API docs: https://developers.terminal49.com
/// Auth:     Authorization: Token {api_key}
/// Base URL: https://api.terminal49.com/api/v2
///
/// The standout capability vs carrier APIs is the real-time "container.available"
/// event that fires when ALL of the following are true:
///   - Container has been discharged from the vessel
///   - Container is grounded in the terminal yard (accessible to trucks)
///   - All holds (freight, customs, USDA) are released
///
/// This is the trigger for dispatching a trucker — carrier APIs often lag 4-12h.
/// </summary>
public class Terminal49Provider : ICarrierTrackingProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<Terminal49Provider> _logger;

    private const string ApiBase = "https://api.terminal49.com/api/v2";

    public string ProviderCode => "TERMINAL49";
    public string ProviderName => "Terminal49";
    public string CarrierCode => "T49";
    public TrackingProviderType ProviderType => TrackingProviderType.Carrier;
    public bool IsAvailable => true;

    public Terminal49Provider(HttpClient http, ILogger<Terminal49Provider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        if (!TryGetApiKey(credentials, out var apiKey))
            return Enumerable.Empty<NormalizedTrackingEvent>();

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ApiBase}/trackings?filter[container_number]={Uri.EscapeDataString(containerNumber)}&include=events");
            req.Headers.Add("Authorization", $"Token {apiKey}");
            req.Headers.Add("Accept", "application/vnd.api+json");

            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Terminal49 returned {Status} for container {Container}",
                    res.StatusCode, containerNumber);
                return Enumerable.Empty<NormalizedTrackingEvent>();
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, containerNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal49 request failed for {Container}", containerNumber);
            return Enumerable.Empty<NormalizedTrackingEvent>();
        }
    }

    public async Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        if (!TryGetApiKey(credentials, out var apiKey))
            return Enumerable.Empty<NormalizedTrackingEvent>();

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{ApiBase}/trackings?filter[ref_number]={Uri.EscapeDataString(bolNumber)}&include=events");
            req.Headers.Add("Authorization", $"Token {apiKey}");
            req.Headers.Add("Accept", "application/vnd.api+json");

            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
                return Enumerable.Empty<NormalizedTrackingEvent>();

            var json = await res.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, bolNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal49 BOL request failed for {Bol}", bolNumber);
            return Enumerable.Empty<NormalizedTrackingEvent>();
        }
    }

    public async Task<IEnumerable<NormalizedTrackingEvent>> GetShipmentEventsAsync(
        string shipmentReference,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
        => await GetBolEventsAsync(shipmentReference, credentials, ct);

    public async Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/trackings?page[size]=1");
            req.Headers.Add("Accept", "application/vnd.api+json");
            var res = await _http.SendAsync(req, ct);
            sw.Stop();
            // 401 Unauthorized still means the API is reachable
            var healthy = res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.Unauthorized;
            return new ProviderHealthResult { IsHealthy = healthy, ResponseTime = sw.Elapsed };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProviderHealthResult { IsHealthy = false, Message = ex.Message, ResponseTime = sw.Elapsed };
        }
    }

    // ─── Response parsing ──────────────────────────────────────────────────

    private IEnumerable<NormalizedTrackingEvent> ParseResponse(string json, string containerNumber)
    {
        var events = new List<NormalizedTrackingEvent>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataArr) || dataArr.ValueKind != JsonValueKind.Array)
                return events;

            // Build an index of included resources (events) by id
            var includedById = BuildIncludedIndex(root);

            foreach (var tracking in dataArr.EnumerateArray())
            {
                if (!tracking.TryGetProperty("attributes", out var attrs)) continue;
                var containerNum = attrs.TryGetProperty("container_number", out var cn) ? cn.GetString() : containerNumber;

                // Extract terminal-level context for enriching events
                var terminalName = GetNestedString(attrs, "pod_terminal", "name")
                                ?? GetNestedString(attrs, "pod_terminal", "nickname");
                var portName = GetNestedString(attrs, "port_of_discharge", "name");
                var portCode = GetNestedString(attrs, "port_of_discharge", "locode");
                var pickupLfd = GetString(attrs, "pickup_lfd");
                var perDiemLfd = GetString(attrs, "per_diem_lfd");
                var vesselName = GetString(attrs, "vessel_name");
                var voyageNumber = GetString(attrs, "voyage_number");
                var isAvailable = GetBool(attrs, "available_for_pickup");

                // Parse all timeline events from the included array
                if (tracking.TryGetProperty("relationships", out var rels)
                    && rels.TryGetProperty("events", out var evtRel)
                    && evtRel.TryGetProperty("data", out var evtRefs)
                    && evtRefs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var evtRef in evtRefs.EnumerateArray())
                    {
                        var evtId = evtRef.TryGetProperty("id", out var eid) ? eid.GetString() : null;
                        if (evtId == null || !includedById.TryGetValue(evtId, out var evtData)) continue;

                        var normalized = ParseSingleEvent(
                            evtData, containerNum ?? containerNumber,
                            terminalName, portName, portCode,
                            vesselName, voyageNumber);

                        if (normalized != null) events.Add(normalized);
                    }
                }

                // If container is currently available and we don't have an explicit
                // container.available event from the events list, synthesise one from
                // the snapshot attributes so the status is always current.
                var hasAvailableEvent = events.Any(e => e.EventType == TrackingEventType.TerminalAvailable);
                if (isAvailable && !hasAvailableEvent)
                {
                    events.Add(BuildAvailableEvent(
                        containerNum ?? containerNumber,
                        terminalName, portName, portCode,
                        pickupLfd, perDiemLfd,
                        GetHoldsInfo(attrs),
                        DateTime.UtcNow));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Terminal49 response for {Container}", containerNumber);
        }

        return events;
    }

    private NormalizedTrackingEvent? ParseSingleEvent(
        JsonElement evtData, string containerNumber,
        string? terminalName, string? portName, string? portCode,
        string? vesselName, string? voyageNumber)
    {
        if (!evtData.TryGetProperty("attributes", out var a)) return null;

        var eventName = GetString(a, "event_name");
        var occurredAt = ParseDateTime(GetString(a, "occurred_at"));
        var location = GetString(a, "location") ?? terminalName ?? portName;
        var eventId = evtData.TryGetProperty("id", out var eid) ? eid.GetString() : null;

        var eventType = MapT49EventName(eventName);
        var description = BuildEventDescription(eventName, eventType, a, terminalName);

        return new NormalizedTrackingEvent
        {
            ExternalId = eventId,
            ProviderCode = ProviderCode,
            ProviderType = ProviderType,
            ContainerNumber = containerNumber,
            EventType = eventType,
            StatusAfter = TrackingNormalizer.MapEventTypeToStatus(eventType),
            EventTime = occurredAt,
            Description = description,
            Location = location,
            LocationCode = portCode,
            VesselName = GetString(a, "vessel_name") ?? vesselName,
            VoyageNumber = GetString(a, "voyage_number") ?? voyageNumber,
            RawData = new Dictionary<string, string>
            {
                ["event_name"] = eventName ?? "",
                ["terminal"] = terminalName ?? "",
                ["port"] = portCode ?? ""
            }
        };
    }

    private NormalizedTrackingEvent BuildAvailableEvent(
        string containerNumber, string? terminal, string? portName, string? portCode,
        string? pickupLfd, string? perDiemLfd, string holdsInfo, DateTime at)
    {
        var lfdPart = pickupLfd != null ? $" | LFD: {pickupLfd}" : "";
        var perDiemPart = perDiemLfd != null ? $" | Per-diem LFD: {perDiemLfd}" : "";
        var terminalPart = terminal != null ? $" at {terminal}" : "";

        return new NormalizedTrackingEvent
        {
            ProviderCode = ProviderCode,
            ProviderType = ProviderType,
            ContainerNumber = containerNumber,
            EventType = TrackingEventType.TerminalAvailable,
            StatusAfter = ContainerStatus.AvailableForPickup,
            EventTime = at,
            Description = $"Container available for pickup{terminalPart}{lfdPart}{perDiemPart} | {holdsInfo}",
            Location = terminal ?? portName,
            LocationCode = portCode,
            RawData = new Dictionary<string, string>
            {
                ["pickup_lfd"] = pickupLfd ?? "",
                ["per_diem_lfd"] = perDiemLfd ?? "",
                ["terminal"] = terminal ?? "",
                ["holds"] = holdsInfo
            }
        };
    }

    // ─── Event mapping ─────────────────────────────────────────────────────

    private static TrackingEventType MapT49EventName(string? name) =>
        name?.ToLowerInvariant() switch
        {
            "vessel.arrived"           => TrackingEventType.VesselArrived,
            "vessel.berthed"           => TrackingEventType.VesselArrived,
            "vessel.departed"          => TrackingEventType.VesselDeparted,
            "container.discharged"     => TrackingEventType.ContainerDischarged,
            "container.grounded"       => TrackingEventType.ContainerDischarged,
            "container.available"      => TrackingEventType.TerminalAvailable,
            "container.loaded"         => TrackingEventType.ContainerLoaded,
            "container.in_gated"       => TrackingEventType.ContainerGateIn,
            "container.out_gated"      => TrackingEventType.ContainerGateOut,
            "container.out_gated_full" => TrackingEventType.ContainerGateOut,
            "container.out_gated_empty"=> TrackingEventType.ContainerGateOut,
            "hold.placed"              => TrackingEventType.HoldPlaced,
            "hold.released"            => TrackingEventType.HoldReleased,
            "customs.released"         => TrackingEventType.CustomsCleared,
            "eta.updated"              => TrackingEventType.EtaUpdated,
            "delay.detected"           => TrackingEventType.DelayNotification,
            _                          => TrackingEventType.PortCallEvent
        };

    private static string BuildEventDescription(
        string? eventName, TrackingEventType eventType, JsonElement attrs, string? terminal)
    {
        // For available events, include the LFD and hold status
        if (eventType == TrackingEventType.TerminalAvailable)
        {
            var lfd = GetString(attrs, "pickup_lfd");
            var perDiem = GetString(attrs, "per_diem_lfd");
            var parts = new List<string> { "Container available for pickup" };
            if (terminal != null) parts.Add($"at {terminal}");
            if (lfd != null) parts.Add($"LFD: {lfd}");
            if (perDiem != null) parts.Add($"per-diem LFD: {perDiem}");
            return string.Join(" | ", parts);
        }

        // For hold events, include the hold type
        if (eventType is TrackingEventType.HoldPlaced or TrackingEventType.HoldReleased)
        {
            var holdType = GetString(attrs, "hold_type") ?? GetString(attrs, "hold_name");
            var action = eventType == TrackingEventType.HoldPlaced ? "placed" : "released";
            return holdType != null ? $"{holdType} hold {action}" : $"Hold {action}";
        }

        // Default: use the description from the API or format the event name
        return GetString(attrs, "description")
            ?? FormatEventName(eventName);
    }

    // ─── Holds parsing ─────────────────────────────────────────────────────

    private static string GetHoldsInfo(JsonElement attrs)
    {
        if (!attrs.TryGetProperty("holds", out var holds)) return "No holds";

        var active = new List<string>();
        foreach (var hold in holds.EnumerateObject())
        {
            if (hold.Value.ValueKind == JsonValueKind.True)
                active.Add(FormatHoldName(hold.Name));
        }
        return active.Count == 0 ? "No holds" : $"Holds: {string.Join(", ", active)}";
    }

    private static string FormatHoldName(string name) => name switch
    {
        "freight_hold" => "Freight",
        "customs_hold" => "Customs",
        "usda_hold"    => "USDA",
        "other_hold"   => "Other",
        _ => name
    };

    // ─── Utilities ─────────────────────────────────────────────────────────

    private static Dictionary<string, JsonElement> BuildIncludedIndex(JsonElement root)
    {
        var index = new Dictionary<string, JsonElement>();
        if (!root.TryGetProperty("included", out var included) || included.ValueKind != JsonValueKind.Array)
            return index;

        foreach (var item in included.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id) && id.GetString() is { } idStr)
                index[idStr] = item;
        }
        return index;
    }

    private static string? GetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? GetNestedString(JsonElement el, string outerKey, string innerKey)
    {
        if (!el.TryGetProperty(outerKey, out var outer) || outer.ValueKind != JsonValueKind.Object) return null;
        return GetString(outer, innerKey);
    }

    private static bool GetBool(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTime ParseDateTime(string? s)
        => s != null && DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime() : DateTime.UtcNow;

    private static string FormatEventName(string? name)
        => name == null ? "Tracking event"
            : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                name.Replace('.', ' ').Replace('_', ' '));

    private bool TryGetApiKey(Dictionary<string, string>? creds, out string apiKey)
    {
        if (creds != null && creds.TryGetValue("api_key", out var key) && !string.IsNullOrWhiteSpace(key))
        {
            apiKey = key;
            return true;
        }
        _logger.LogDebug("Terminal49 skipped — no api_key credential configured");
        apiKey = "";
        return false;
    }
}
