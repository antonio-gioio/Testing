namespace ContainerTracking.Core.Entities;

public class AuditLog : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool Success { get; set; } = true;
    public string? FailureReason { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
