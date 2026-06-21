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
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly ICurrentOrganizationContext _orgContext;

    public DashboardController(AppDbContext db, ITierEnforcementService tierService, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _orgContext = orgContext;
    }

    /// <summary>GET /api/v1/dashboard/layouts — list dashboard layouts for user/org</summary>
    [HttpGet("layouts")]
    public async Task<IActionResult> GetLayouts(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var userId = GetUserId();

        var layouts = await _db.DashboardLayouts
            .Include(l => l.Widgets)
            .Where(l => l.OrganizationId == orgId &&
                        (l.IsOrganizationDefault || l.UserId == userId))
            .ToListAsync(ct);

        return Ok(layouts);
    }

    /// <summary>POST /api/v1/dashboard/layouts — create a new dashboard layout</summary>
    [HttpPost("layouts")]
    public async Task<IActionResult> CreateLayout([FromBody] CreateLayoutRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var userId = GetUserId();

        var layout = new DashboardLayout
        {
            OrganizationId = orgId,
            UserId = userId,
            Name = request.Name,
            Columns = request.Columns,
            Theme = request.Theme,
            IsOrganizationDefault = request.IsOrganizationDefault &&
                (_orgContext.Role == Roles.OrganizationAdmin || _orgContext.IsPlatformAdmin)
        };

        _db.DashboardLayouts.Add(layout);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetLayout), new { id = layout.Id }, layout);
    }

    /// <summary>GET /api/v1/dashboard/layouts/{id}</summary>
    [HttpGet("layouts/{id:guid}")]
    public async Task<IActionResult> GetLayout(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var layout = await _db.DashboardLayouts
            .Include(l => l.Widgets)
            .FirstOrDefaultAsync(l => l.Id == id && l.OrganizationId == orgId, ct);

        if (layout == null) return NotFound();
        return Ok(layout);
    }

    /// <summary>PUT /api/v1/dashboard/layouts/{id}/widgets — replace all widgets for a layout</summary>
    [HttpPut("layouts/{id:guid}/widgets")]
    public async Task<IActionResult> UpdateWidgets(Guid id, [FromBody] List<WidgetConfig> widgets, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var layout = await _db.DashboardLayouts
            .Include(l => l.Widgets)
            .FirstOrDefaultAsync(l => l.Id == id && l.OrganizationId == orgId, ct);

        if (layout == null) return NotFound();

        var limits = await _tierService.GetLimitsAsync(orgId, ct);

        var customWidgets = widgets.Where(w => w.WidgetType >= WidgetType.KpiSummary).ToList();
        if (customWidgets.Any() && !limits.CustomWidgetsEnabled)
            return StatusCode(402, new { error = "Custom widgets require Business tier or higher." });

        _db.DashboardWidgets.RemoveRange(layout.Widgets);

        foreach (var w in widgets)
        {
            layout.Widgets.Add(new DashboardWidget
            {
                DashboardLayoutId = layout.Id,
                WidgetType = w.WidgetType,
                Title = w.Title,
                Column = w.Column,
                Row = w.Row,
                Width = w.Width,
                Height = w.Height,
                Config = w.Config,
                RefreshIntervalSeconds = w.RefreshIntervalSeconds
            });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(layout);
    }

    /// <summary>GET /api/v1/dashboard/summary — KPI summary data for dashboard</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var orgId = GetOrgId();

        var total = await _db.Containers.CountAsync(c => c.OrganizationId == orgId, ct);
        var inTransit = await _db.Containers.CountAsync(c => c.OrganizationId == orgId && c.Status == ContainerStatus.InTransit, ct);
        var arrived = await _db.Containers.CountAsync(c => c.OrganizationId == orgId && c.Status == ContainerStatus.Arrived, ct);
        var delayed = await _db.Containers.CountAsync(c => c.OrganizationId == orgId && c.Status == ContainerStatus.Delayed, ct);
        var recentEvents = await _db.TrackingEvents
            .Where(e => e.OrganizationId == orgId && e.EventTime >= DateTime.UtcNow.AddHours(-24))
            .CountAsync(ct);

        var tierUsage = await _tierService.GetUsageSummaryAsync(orgId, ct);

        return Ok(new
        {
            containers = new { total, inTransit, arrived, delayed },
            recentEventsLast24h = recentEvents,
            tierUsage
        });
    }

    /// <summary>GET /api/v1/dashboard/containers-by-status — for pie/bar charts</summary>
    [HttpGet("containers-by-status")]
    public async Task<IActionResult> GetContainersByStatus(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var grouped = await _db.Containers
            .Where(c => c.OrganizationId == orgId)
            .GroupBy(c => c.Status)
            .Select(g => new { status = g.Key.ToString(), count = g.Count() })
            .ToListAsync(ct);

        return Ok(grouped);
    }

    /// <summary>GET /api/v1/dashboard/map-data — all container positions for map widget</summary>
    [HttpGet("map-data")]
    public async Task<IActionResult> GetMapData(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var containers = await _db.Containers
            .Where(c => c.OrganizationId == orgId && c.CurrentPosition != null)
            .Select(c => new
            {
                id = c.Id,
                containerNumber = c.ContainerNumber,
                status = c.Status.ToString(),
                latitude = c.CurrentPosition!.Y,
                longitude = c.CurrentPosition.X,
                location = c.CurrentLocation
            })
            .ToListAsync(ct);

        var vessels = await _db.Vessels
            .Where(v => v.CurrentPosition != null)
            .Select(v => new
            {
                imo = v.Imo,
                name = v.Name,
                latitude = v.CurrentPosition!.Y,
                longitude = v.CurrentPosition.X,
                speed = v.SpeedKnots,
                heading = v.Heading
            })
            .Take(100)
            .ToListAsync(ct);

        return Ok(new { containers, vessels });
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

public record CreateLayoutRequest(string Name, int Columns = 12, string? Theme = null, bool IsOrganizationDefault = false);
public record WidgetConfig(WidgetType WidgetType, string Title, int Column, int Row, int Width, int Height,
    Dictionary<string, object> Config, int RefreshIntervalSeconds = 60);
