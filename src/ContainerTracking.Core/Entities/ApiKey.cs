namespace ContainerTracking.Core.Entities;

public class ApiKey : OrganizationScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string HashedKey { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public int CallCount { get; set; } = 0;
    public string? AllowedIpAddresses { get; set; }
}
