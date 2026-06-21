using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/tracking-events")]
[Authorize]
public class TrackingEventsController(AppDbContext db, ICurrentOrganizationContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? containerId,
        [FromQuery] Guid? shipmentId,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 50,
        [FromQuery] int page = 1)
    {
        var orgId = ctx.OrganizationId;
        limit = Math.Clamp(limit, 1, 200);

        var query = db.TrackingEvents
            .Include(e => e.Container)
            .Where(e => e.OrganizationId == orgId && !e.IsDeleted);

        if (containerId.HasValue) query = query.Where(e => e.ContainerId == containerId);
        if (shipmentId.HasValue) query = query.Where(e => e.ShipmentId == shipmentId);
        if (!string.IsNullOrEmpty(eventType) && Enum.TryParse<Core.Enums.TrackingEventType>(eventType, out var et))
            query = query.Where(e => e.EventType == et);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.EventTime)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(e => new
            {
                e.Id,
                ContainerNumber = e.Container != null ? e.Container.ContainerNumber : null,
                e.ContainerId,
                e.ShipmentId,
                EventType = e.EventType.ToString(),
                ProviderType = e.ProviderType.ToString(),
                e.Description,
                e.Location,
                e.LocationCode,
                OccurredAt = e.EventTime,
                e.VesselName,
                e.VoyageNumber,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, limit });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var orgId = ctx.OrganizationId;
        var evt = await db.TrackingEvents
            .Include(e => e.Container)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId && !e.IsDeleted);
        if (evt == null) return NotFound();

        return Ok(new
        {
            evt.Id,
            ContainerNumber = evt.Container?.ContainerNumber,
            evt.ContainerId, evt.ShipmentId, evt.BillOfLadingId,
            EventType = evt.EventType.ToString(),
            ProviderType = evt.ProviderType.ToString(),
            evt.Description, evt.Location, evt.LocationCode, evt.CountryCode,
            evt.VesselName, evt.VesselImo, evt.VoyageNumber,
            evt.SpeedKnots, evt.Heading,
            OccurredAt = evt.EventTime,
            Latitude = evt.GeoPosition?.Y,
            Longitude = evt.GeoPosition?.X,
            evt.CreatedAt
        });
    }
}
