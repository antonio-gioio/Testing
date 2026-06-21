namespace ContainerTracking.Core.Models;

public class TierLimits
{
    public int MaxContainers { get; set; }
    public int MaxUsers { get; set; }
    public int MaxShipments { get; set; }
    public int MaxAlerts { get; set; }
    public int UpdateIntervalMinutes { get; set; }
    public int HistoryRetentionDays { get; set; }
    public bool WebSocketEnabled { get; set; }
    public int ApiRateLimitPerHour { get; set; }
    public bool ExportEnabled { get; set; }
    public bool AdvancedAnalyticsEnabled { get; set; }
    public bool CustomWidgetsEnabled { get; set; }
    public bool WhiteLabelEnabled { get; set; }
    public bool ApiAccessEnabled { get; set; }
    public bool CsvImportEnabled { get; set; }
    public bool WebhookAlertsEnabled { get; set; }
    public bool CustomerTrackingLinksEnabled { get; set; }
    public string TierName { get; set; } = string.Empty;
    public string SlaLevel { get; set; } = string.Empty;
}

public class TierCheckResult
{
    public bool Allowed { get; set; }
    public string? DenialReason { get; set; }
    public int? CurrentUsage { get; set; }
    public int? Limit { get; set; }

    public static TierCheckResult Allow() => new() { Allowed = true };
    public static TierCheckResult Deny(string reason, int? current = null, int? limit = null) =>
        new() { Allowed = false, DenialReason = reason, CurrentUsage = current, Limit = limit };
}

public class TierUsageSummary
{
    public Guid OrganizationId { get; set; }
    public string TierName { get; set; } = string.Empty;
    public int Containers { get; set; }
    public int MaxContainers { get; set; }
    public int Users { get; set; }
    public int MaxUsers { get; set; }
    public int Shipments { get; set; }
    public int MaxShipments { get; set; }
    public int Alerts { get; set; }
    public int MaxAlerts { get; set; }
    public int ApiCallsThisHour { get; set; }
    public int ApiRateLimitPerHour { get; set; }
    public double ContainerUsagePercent => MaxContainers > 0 ? (double)Containers / MaxContainers * 100 : 0;
    public double UserUsagePercent => MaxUsers > 0 ? (double)Users / MaxUsers * 100 : 0;
    public double ApiUsagePercent => ApiRateLimitPerHour > 0 ? (double)ApiCallsThisHour / ApiRateLimitPerHour * 100 : 0;
}
