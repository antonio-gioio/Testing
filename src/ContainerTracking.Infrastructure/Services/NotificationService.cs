using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext db, HttpClient http, ILogger<NotificationService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    public async Task SendInAppNotificationAsync(
        Guid organizationId, Guid? userId, string title, string message,
        AlertSeverity severity, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            OrganizationId = organizationId,
            UserId = userId,
            Title = title,
            Message = message,
            Severity = severity,
            Channel = NotificationChannel.InApp,
            Status = NotificationStatus.Sent,
            SentAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SendEmailNotificationAsync(string email, string subject, string body, CancellationToken ct = default)
    {
        // In production: use SendGrid, AWS SES, or SMTP
        _logger.LogInformation("Email notification to {Email}: {Subject}", email, subject);
        await Task.CompletedTask;
    }

    public async Task SendWebhookNotificationAsync(string webhookUrl, string secret, object payload, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var signature = ComputeHmacSha256(json, secret);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = content
            };
            request.Headers.Add("X-CT-Signature", signature);
            request.Headers.Add("X-CT-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            var response = await _http.SendAsync(request, ct);
            _logger.LogInformation("Webhook to {Url} returned {Status}", webhookUrl, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook delivery failed to {Url}", webhookUrl);
        }
    }

    public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId && n.Status != NotificationStatus.Read)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification != null)
        {
            notification.Status = NotificationStatus.Read;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string ComputeHmacSha256(string message, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
