using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? WebsiteUrl { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsWhiteLabel { get; set; } = false;
    public string? CustomDomain { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();

    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<Container> Containers { get; set; } = new List<Container>();
    public ICollection<Vessel> Vessels { get; set; } = new List<Vessel>();
    public ICollection<Alert> Alerts { get; set; } = new List<Alert>();
    public ICollection<DashboardLayout> DashboardLayouts { get; set; } = new List<DashboardLayout>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<ProviderCredential> ProviderCredentials { get; set; } = new List<ProviderCredential>();
    public ICollection<ExportJob> ExportJobs { get; set; } = new List<ExportJob>();
    public OrganizationSubscription? Subscription { get; set; }
    public TierUsageCounter? UsageCounter { get; set; }
}
