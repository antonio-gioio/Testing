using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class TrackingProvider : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public TrackingProviderType ProviderType { get; set; }
    public string? Description { get; set; }
    public string? BaseUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsGlobal { get; set; } = false;
    public bool RequiresCredentials { get; set; } = true;
    public int PollIntervalMinutes { get; set; } = 60;
    public int Priority { get; set; } = 0;
    public Dictionary<string, string> ConfigSchema { get; set; } = new();

    public ICollection<ProviderCredential> Credentials { get; set; } = new List<ProviderCredential>();
}

public class ProviderCredential : OrganizationScopedEntity
{
    public Guid TrackingProviderId { get; set; }
    public TrackingProvider TrackingProvider { get; set; } = null!;
    public string EncryptedApiKey { get; set; } = string.Empty;
    public string? EncryptedApiSecret { get; set; }
    public Dictionary<string, string> EncryptedConfig { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
}
