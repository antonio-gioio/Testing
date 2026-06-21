using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Services;

public class AlertProcessingService(AppDbContext db, INotificationService notificationService, ILogger<AlertProcessingService> logger)
{
    public async Task ProcessEventAsync(TrackingEvent trackingEvent, Container? container, Shipment? shipment)
    {
        var orgId = trackingEvent.OrganizationId;
        var alerts = await db.Alerts
            .Where(a => a.OrganizationId == orgId && a.IsEnabled && !a.IsDeleted)
            .ToListAsync();

        foreach (var alert in alerts)
        {
            try
            {
                if (!ShouldTrigger(alert, trackingEvent, container, shipment)) continue;
                if (IsInCooldown(alert)) continue;

                await TriggerAlertAsync(alert, trackingEvent, container, shipment);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating alert {AlertId} for event {EventId}", alert.Id, trackingEvent.Id);
            }
        }
    }

    private static bool ShouldTrigger(Alert alert, TrackingEvent evt, Container? container, Shipment? shipment)
    {
        return alert.TriggerType switch
        {
            AlertTriggerType.StatusChange => evt.ContainerStatusAfter.HasValue,
            AlertTriggerType.DelayDetected => shipment?.IsDelayDetected == true || evt.EventType == TrackingEventType.DelayNotification,
            AlertTriggerType.EtaChanged => evt.EventType == TrackingEventType.EtaUpdated,
            AlertTriggerType.PortArrival => evt.EventType == TrackingEventType.VesselArrived || evt.EventType == TrackingEventType.PortCallEvent,
            AlertTriggerType.PortDeparture => evt.EventType == TrackingEventType.VesselDeparted,
            AlertTriggerType.VesselDiverted => evt.EventType == TrackingEventType.DelayNotification,
            _ => false
        };
    }

    private static bool IsInCooldown(Alert alert)
    {
        if (!alert.LastTriggeredAt.HasValue) return false;
        return (DateTime.UtcNow - alert.LastTriggeredAt.Value).TotalMinutes < alert.CooldownMinutes;
    }

    private async Task TriggerAlertAsync(Alert alert, TrackingEvent evt, Container? container, Shipment? shipment)
    {
        alert.LastTriggeredAt = DateTime.UtcNow;
        alert.TriggerCount++;
        alert.UpdatedAt = DateTime.UtcNow;

        var title = BuildTitle(alert, evt);
        var message = BuildMessage(alert, evt, container, shipment);

        foreach (var channel in alert.NotificationChannels)
        {
            if (!Enum.TryParse<NotificationChannel>(channel, out var ch)) continue;

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                AlertId = alert.Id,
                OrganizationId = alert.OrganizationId,
                Title = title,
                Message = message,
                Severity = alert.Severity,
                Channel = ch,
                Status = NotificationStatus.Pending,
                RelatedEntityType = container != null ? "Container" : "Shipment",
                RelatedEntityId = container?.Id ?? shipment?.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Notifications.Add(notification);

            try
            {
                await DispatchNotificationAsync(notification, alert, ch);
                notification.Status = NotificationStatus.Sent;
                notification.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                notification.Status = NotificationStatus.Failed;
                notification.FailureReason = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                logger.LogWarning(ex, "Failed to send {Channel} notification for alert {AlertId}", ch, alert.Id);
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task DispatchNotificationAsync(Notification notification, Alert alert, NotificationChannel channel)
    {
        switch (channel)
        {
            case NotificationChannel.InApp:
                await notificationService.SendInAppNotificationAsync(
                    alert.OrganizationId, null, notification.Title, notification.Message, alert.Severity);
                break;

            case NotificationChannel.Email when alert.RecipientEmails.Count > 0:
                foreach (var email in alert.RecipientEmails)
                    await notificationService.SendEmailNotificationAsync(email, notification.Title, notification.Message);
                break;

            case NotificationChannel.Webhook when !string.IsNullOrEmpty(alert.WebhookUrl):
                await notificationService.SendWebhookNotificationAsync(
                    alert.WebhookUrl,
                    alert.WebhookSecret ?? "",
                    new { title = notification.Title, message = notification.Message, severity = alert.Severity.ToString(), triggeredAt = DateTime.UtcNow });
                break;
        }
    }

    private static string BuildTitle(Alert alert, TrackingEvent evt) =>
        $"[{alert.Severity}] {alert.Name}";

    private static string BuildMessage(Alert alert, TrackingEvent evt, Container? container, Shipment? shipment)
    {
        var subject = container != null ? $"Container {container.ContainerNumber}" : shipment != null ? $"Shipment {shipment.Reference}" : "Unknown";
        return $"{subject}: {(string.IsNullOrEmpty(evt.Description) ? alert.TriggerType.ToString() : evt.Description)} at {evt.Location ?? "unknown location"} ({evt.EventTime:dd MMM yyyy HH:mm} UTC)";
    }
}
