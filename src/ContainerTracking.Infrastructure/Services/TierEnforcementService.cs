using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Services;

public class TierEnforcementService : ITierEnforcementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TierEnforcementService> _logger;

    public TierEnforcementService(AppDbContext db, ILogger<TierEnforcementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TierLimits> GetLimitsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var sub = await _db.OrganizationSubscriptions
            .Include(s => s.SubscriptionTier)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        if (sub?.SubscriptionTier == null)
        {
            return GetFreeTierLimits();
        }

        var tier = sub.SubscriptionTier;
        var limits = new TierLimits
        {
            TierName = tier.Name,
            MaxContainers = ApplyOverride(sub.CustomLimitOverrides, "MaxContainers", tier.MaxContainers),
            MaxUsers = ApplyOverride(sub.CustomLimitOverrides, "MaxUsers", tier.MaxUsers),
            MaxShipments = ApplyOverride(sub.CustomLimitOverrides, "MaxShipments", tier.MaxShipments),
            MaxAlerts = ApplyOverride(sub.CustomLimitOverrides, "MaxAlerts", tier.MaxAlerts),
            UpdateIntervalMinutes = tier.UpdateIntervalMinutes,
            HistoryRetentionDays = tier.HistoryRetentionDays,
            WebSocketEnabled = tier.WebSocketEnabled,
            ApiRateLimitPerHour = ApplyOverride(sub.CustomLimitOverrides, "ApiRateLimitPerHour", tier.ApiRateLimitPerHour),
            ExportEnabled = tier.ExportEnabled,
            AdvancedAnalyticsEnabled = tier.AdvancedAnalyticsEnabled,
            CustomWidgetsEnabled = tier.CustomWidgetsEnabled,
            WhiteLabelEnabled = tier.WhiteLabelEnabled,
            ApiAccessEnabled = tier.ApiAccessEnabled,
            CsvImportEnabled = tier.CsvImportEnabled,
            WebhookAlertsEnabled = tier.WebhookAlertsEnabled,
            CustomerTrackingLinksEnabled = tier.CustomerTrackingLinksEnabled,
            SlaLevel = tier.SlaLevel
        };

        return limits;
    }

    public async Task<TierCheckResult> CanAddContainerAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);
        var current = usage?.ActiveContainers ?? 0;

        if (current >= limits.MaxContainers)
            return TierCheckResult.Deny($"Container limit of {limits.MaxContainers} reached. Upgrade to add more.", current, limits.MaxContainers);

        return TierCheckResult.Allow();
    }

    public async Task<TierCheckResult> CanAddUserAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);
        var current = usage?.ActiveUsers ?? 0;

        if (current >= limits.MaxUsers)
            return TierCheckResult.Deny($"User limit of {limits.MaxUsers} reached. Upgrade to add more.", current, limits.MaxUsers);

        return TierCheckResult.Allow();
    }

    public async Task<TierCheckResult> CanAddAlertAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);
        var current = usage?.ActiveAlerts ?? 0;

        if (current >= limits.MaxAlerts)
            return TierCheckResult.Deny($"Alert limit of {limits.MaxAlerts} reached. Upgrade to add more.", current, limits.MaxAlerts);

        return TierCheckResult.Allow();
    }

    public async Task<TierCheckResult> CanExportAsync(Guid organizationId, ExportFormat format, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        if (!limits.ExportEnabled)
            return TierCheckResult.Deny("Export is not available on your current tier. Upgrade to access exports.");

        if (format == ExportFormat.Pdf && !limits.AdvancedAnalyticsEnabled)
            return TierCheckResult.Deny("PDF export requires Business tier or higher.");

        return TierCheckResult.Allow();
    }

    public async Task<TierCheckResult> CanAccessApiAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        if (!limits.ApiAccessEnabled)
            return TierCheckResult.Deny("API access is not available on your current tier.");

        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);
        var current = usage?.ApiCallsThisHour ?? 0;
        if (current >= limits.ApiRateLimitPerHour)
            return TierCheckResult.Deny($"API rate limit of {limits.ApiRateLimitPerHour} calls/hour reached.", current, limits.ApiRateLimitPerHour);

        return TierCheckResult.Allow();
    }

    public async Task<TierCheckResult> CanUseWebSocketAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        if (!limits.WebSocketEnabled)
            return TierCheckResult.Deny("Real-time WebSocket updates require Starter tier or higher.");

        return TierCheckResult.Allow();
    }

    public async Task<TierUsageSummary> GetUsageSummaryAsync(Guid organizationId, CancellationToken ct = default)
    {
        var limits = await GetLimitsAsync(organizationId, ct);
        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);

        return new TierUsageSummary
        {
            OrganizationId = organizationId,
            TierName = limits.TierName,
            Containers = usage?.ActiveContainers ?? 0,
            MaxContainers = limits.MaxContainers,
            Users = usage?.ActiveUsers ?? 0,
            MaxUsers = limits.MaxUsers,
            Shipments = usage?.ActiveShipments ?? 0,
            MaxShipments = limits.MaxShipments,
            Alerts = usage?.ActiveAlerts ?? 0,
            MaxAlerts = limits.MaxAlerts,
            ApiCallsThisHour = usage?.ApiCallsThisHour ?? 0,
            ApiRateLimitPerHour = limits.ApiRateLimitPerHour
        };
    }

    public async Task IncrementApiCallAsync(Guid organizationId, CancellationToken ct = default)
    {
        var usage = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == organizationId, ct);
        if (usage != null)
        {
            usage.ApiCallsThisHour++;
            usage.ApiCallsToday++;
            usage.LastApiCallAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private TierLimits GetFreeTierLimits() => new()
    {
        TierName = "Free",
        MaxContainers = 5,
        MaxUsers = 2,
        MaxShipments = 3,
        MaxAlerts = 2,
        UpdateIntervalMinutes = 360,
        HistoryRetentionDays = 7,
        WebSocketEnabled = false,
        ApiRateLimitPerHour = 0,
        ExportEnabled = false,
        AdvancedAnalyticsEnabled = false,
        CustomWidgetsEnabled = false,
        WhiteLabelEnabled = false,
        ApiAccessEnabled = false,
        CsvImportEnabled = false,
        WebhookAlertsEnabled = false,
        CustomerTrackingLinksEnabled = false,
        SlaLevel = "Community"
    };

    private int ApplyOverride(Dictionary<string, int> overrides, string key, int defaultValue)
    {
        return overrides.TryGetValue(key, out var val) ? val : defaultValue;
    }
}
