using System.Net.Http.Json;
using System.Text.Json;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

/// <summary>
/// AIS provider using aisstream.io open WebSocket API.
/// Provides near-real-time vessel positions via AIS data.
/// Note: AIS tracks vessels, not individual containers.
/// </summary>
public class AisStreamProvider : IAisProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<AisStreamProvider> _logger;
    private const string BaseUrl = "https://api.aisstream.io/v0";

    public string ProviderCode => "AISSTREAM";
    public string ProviderName => "AISStream.io";
    public TrackingProviderType ProviderType => TrackingProviderType.Ais;
    public bool IsAvailable => true;

    public AisStreamProvider(HttpClient http, ILogger<AisStreamProvider> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(
        string containerNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        // AIS does not provide container-level tracking — returns empty
        _logger.LogDebug("AIS provider cannot track container {Container} directly", containerNumber);
        return Enumerable.Empty<NormalizedTrackingEvent>();
    }

    public async Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(
        string bolNumber,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        return Enumerable.Empty<NormalizedTrackingEvent>();
    }

    public async Task<VesselPositionResult?> GetVesselPositionByImoAsync(string imo, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/vessels?imo={imo}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseVesselPosition(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vessel position for IMO {Imo}", imo);
            return null;
        }
    }

    public async Task<VesselPositionResult?> GetVesselPositionByMmsiAsync(string mmsi, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/vessels?mmsi={mmsi}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseVesselPosition(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vessel position for MMSI {Mmsi}", mmsi);
            return null;
        }
    }

    public async Task<IEnumerable<VesselPositionResult>> GetVesselsInBoundingBoxAsync(
        double minLat, double minLon, double maxLat, double maxLon,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/vessels?bbox={minLon},{minLat},{maxLon},{maxLat}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return Enumerable.Empty<VesselPositionResult>();

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseVesselList(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get vessels in bounding box");
            return Enumerable.Empty<VesselPositionResult>();
        }
    }

    public async Task<ProviderHealthResult> CheckHealthAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/health", ct);
            sw.Stop();
            return new ProviderHealthResult
            {
                IsHealthy = response.IsSuccessStatusCode,
                ResponseTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProviderHealthResult { IsHealthy = false, Message = ex.Message, ResponseTime = sw.Elapsed };
        }
    }

    private VesselPositionResult? ParseVesselPosition(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) return null;

            return new VesselPositionResult
            {
                Imo = data.GetProperty("imo").GetString() ?? string.Empty,
                Mmsi = data.TryGetProperty("mmsi", out var mmsi) ? mmsi.GetString() : null,
                Name = data.TryGetProperty("name", out var name) ? name.GetString() : null,
                Latitude = data.GetProperty("latitude").GetDouble(),
                Longitude = data.GetProperty("longitude").GetDouble(),
                SpeedKnots = data.TryGetProperty("speed", out var spd) ? spd.GetDouble() : null,
                Heading = data.TryGetProperty("heading", out var hdg) ? hdg.GetDouble() : null,
                Destination = data.TryGetProperty("destination", out var dest) ? dest.GetString() : null,
                NavigationStatus = data.TryGetProperty("nav_status", out var ns) ? ns.GetString() : null,
                Timestamp = data.TryGetProperty("timestamp", out var ts)
                    ? DateTime.Parse(ts.GetString()!)
                    : DateTime.UtcNow
            };
        }
        catch { return null; }
    }

    private IEnumerable<VesselPositionResult> ParseVesselList(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var results = new List<VesselPositionResult>();
            if (!doc.RootElement.TryGetProperty("data", out var data)) return results;

            foreach (var item in data.EnumerateArray())
            {
                var pos = ParseVesselPosition(item.GetRawText());
                if (pos != null) results.Add(pos);
            }
            return results;
        }
        catch { return Enumerable.Empty<VesselPositionResult>(); }
    }
}
