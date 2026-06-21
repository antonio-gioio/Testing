namespace ContainerTracking.Core.Entities;

public class OrganizationSubscription : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid SubscriptionTierId { get; set; }
    public SubscriptionTier SubscriptionTier { get; set; } = null!;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsAnnual { get; set; } = false;
    public string? ExternalSubscriptionId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? TrialEndDate { get; set; }
    public bool IsTrial { get; set; } = false;
    public Dictionary<string, int> CustomLimitOverrides { get; set; } = new();
}
