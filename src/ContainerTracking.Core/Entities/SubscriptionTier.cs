using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class SubscriptionTier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public SubscriptionTierType TierType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPriceUsd { get; set; }
    public decimal AnnualPriceUsd { get; set; }

    public int MaxContainers { get; set; }
    public int MaxUsers { get; set; }
    public int UpdateIntervalMinutes { get; set; } = 60;
    public int HistoryRetentionDays { get; set; } = 30;
    public bool WebSocketEnabled { get; set; } = false;
    public int ApiRateLimitPerHour { get; set; } = 100;
    public bool ExportEnabled { get; set; } = false;
    public bool AdvancedAnalyticsEnabled { get; set; } = false;
    public bool CustomWidgetsEnabled { get; set; } = false;
    public int MaxAlerts { get; set; } = 5;
    public bool WhiteLabelEnabled { get; set; } = false;
    public string SlaLevel { get; set; } = "Community";
    public bool ApiAccessEnabled { get; set; } = false;
    public bool CsvImportEnabled { get; set; } = false;
    public bool WebhookAlertsEnabled { get; set; } = false;
    public bool CustomerTrackingLinksEnabled { get; set; } = false;
    public int MaxShipments { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;

    public ICollection<OrganizationSubscription> Subscriptions { get; set; } = new List<OrganizationSubscription>();
}
