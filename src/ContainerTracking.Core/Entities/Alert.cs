using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class Alert : OrganizationScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public AlertTriggerType TriggerType { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, string> Conditions { get; set; } = new();
    public List<string> NotificationChannels { get; set; } = new();
    public List<string> RecipientEmails { get; set; } = new();
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public Guid? ContainerId { get; set; }
    public Guid? ShipmentId { get; set; }
    public int CooldownMinutes { get; set; } = 60;
    public DateTime? LastTriggeredAt { get; set; }
    public int TriggerCount { get; set; } = 0;

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Notification : BaseEntity
{
    public Guid? AlertId { get; set; }
    public Alert? Alert { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;
    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? FailureReason { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
