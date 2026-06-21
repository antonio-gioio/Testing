using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Models;

namespace ContainerTracking.Core.Interfaces;

public interface ITierEnforcementService
{
    Task<TierCheckResult> CanAddContainerAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierCheckResult> CanAddUserAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierCheckResult> CanAddAlertAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierCheckResult> CanExportAsync(Guid organizationId, ExportFormat format, CancellationToken ct = default);
    Task<TierCheckResult> CanAccessApiAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierCheckResult> CanUseWebSocketAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierLimits> GetLimitsAsync(Guid organizationId, CancellationToken ct = default);
    Task<TierUsageSummary> GetUsageSummaryAsync(Guid organizationId, CancellationToken ct = default);
    Task IncrementApiCallAsync(Guid organizationId, CancellationToken ct = default);
}

public interface IExportService
{
    Task<ExportJob> QueueExportAsync(
        Guid organizationId,
        Guid userId,
        ExportFormat format,
        ExportDataType dataType,
        Dictionary<string, string> filters,
        CancellationToken ct = default);

    Task ProcessExportJobAsync(Guid exportJobId, CancellationToken ct = default);
    Task<string?> GetSecureDownloadUrlAsync(Guid exportJobId, string token, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        Guid? organizationId = null,
        Guid? userId = null,
        object? oldValues = null,
        object? newValues = null,
        bool success = true,
        string? failureReason = null,
        CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendInAppNotificationAsync(Guid organizationId, Guid? userId, string title, string message, AlertSeverity severity, CancellationToken ct = default);
    Task SendEmailNotificationAsync(string email, string subject, string body, CancellationToken ct = default);
    Task SendWebhookNotificationAsync(string webhookUrl, string secret, object payload, CancellationToken ct = default);
    Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
}

public interface ICurrentOrganizationContext
{
    Guid? OrganizationId { get; }
    string? UserId { get; }
    string? Role { get; }
    bool IsPlatformAdmin { get; }
}

public interface IContainerPollingService
{
    /// <summary>
    /// Polls all configured tracking providers for a single container immediately,
    /// stores any new events, and publishes real-time updates.
    /// Returns the count of new events persisted.
    /// </summary>
    Task<int> PollContainerNowAsync(Guid containerId, Guid organizationId, CancellationToken ct = default);
}
