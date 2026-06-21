using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly INotificationService _notifications;
    private readonly ICurrentOrganizationContext _orgContext;

    public AlertsController(AppDbContext db, ITierEnforcementService tierService,
        INotificationService notifications, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _notifications = notifications;
        _orgContext = orgContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var alerts = await _db.Alerts.Where(a => a.OrganizationId == orgId).ToListAsync(ct);
        return Ok(alerts);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> CreateAlert([FromBody] CreateAlertRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();

        var tierCheck = await _tierService.CanAddAlertAsync(orgId, ct);
        if (!tierCheck.Allowed)
            return StatusCode(402, new { error = tierCheck.DenialReason });

        var limits = await _tierService.GetLimitsAsync(orgId, ct);
        if (request.WebhookUrl != null && !limits.WebhookAlertsEnabled)
            return StatusCode(402, new { error = "Webhook alerts require Business tier or higher." });

        var alert = new Alert
        {
            OrganizationId = orgId,
            Name = request.Name,
            TriggerType = request.TriggerType,
            Severity = request.Severity,
            Conditions = request.Conditions,
            NotificationChannels = request.NotificationChannels,
            RecipientEmails = request.RecipientEmails,
            WebhookUrl = request.WebhookUrl,
            ContainerId = request.ContainerId,
            ShipmentId = request.ShipmentId,
            CooldownMinutes = request.CooldownMinutes
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAlert), new { id = alert.Id }, alert);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAlert(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == orgId, ct);
        if (alert == null) return NotFound();
        return Ok(alert);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> DeleteAlert(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == orgId, ct);
        if (alert == null) return NotFound();
        alert.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return BadRequest();
        var notifs = await _notifications.GetUnreadNotificationsAsync(userId.Value, ct);
        return Ok(notifs);
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return BadRequest();
        await _notifications.MarkAsReadAsync(id, userId.Value, ct);
        return Ok();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var unread = await _db.Notifications
            .Where(n => n.OrganizationId == orgId && n.ReadAt == null)
            .ToListAsync(ct);
        foreach (var n in unread)
        {
            n.ReadAt = DateTime.UtcNow;
            n.Status = Core.Enums.NotificationStatus.Read;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { marked = unread.Count });
    }

    private Guid GetOrgId()
    {
        var id = _orgContext.OrganizationId;
        if (!id.HasValue) throw new UnauthorizedAccessException();
        return id.Value;
    }

    private Guid? GetUserId()
    {
        var id = _orgContext.UserId;
        return id != null && Guid.TryParse(id, out var g) ? g : null;
    }
}

public record CreateAlertRequest(
    string Name, AlertTriggerType TriggerType, AlertSeverity Severity,
    Dictionary<string, string> Conditions, List<string> NotificationChannels,
    List<string> RecipientEmails, string? WebhookUrl,
    Guid? ContainerId, Guid? ShipmentId, int CooldownMinutes = 60);
