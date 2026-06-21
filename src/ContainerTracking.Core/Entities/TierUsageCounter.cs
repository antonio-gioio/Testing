namespace ContainerTracking.Core.Entities;

public class TierUsageCounter : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public int ActiveContainers { get; set; } = 0;
    public int ActiveShipments { get; set; } = 0;
    public int ActiveUsers { get; set; } = 0;
    public int ActiveAlerts { get; set; } = 0;
    public int ApiCallsThisHour { get; set; } = 0;
    public int ApiCallsToday { get; set; } = 0;
    public int ExportsThisMonth { get; set; } = 0;
    public DateTime LastApiCallAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReset { get; set; } = DateTime.UtcNow;
}
