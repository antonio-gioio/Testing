using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/vessels")]
[Authorize]
public class VesselsController(AppDbContext db, ICurrentOrganizationContext ctx, IAisProvider ais) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Vessels.Where(v => !v.IsDeleted);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(v => v.Name.Contains(search) || v.Imo.Contains(search) || (v.Mmsi != null && v.Mmsi.Contains(search)));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(v => v.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id, v.Name, v.Imo, v.Mmsi, v.Flag, Type = v.Type,
                v.SpeedKnots, v.Heading, v.NavigationStatus,
                Latitude = v.CurrentPosition == null ? (double?)null : v.CurrentPosition.Y,
                Longitude = v.CurrentPosition == null ? (double?)null : v.CurrentPosition.X,
                v.LastPositionUpdate, v.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var v = await db.Vessels.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (v == null) return NotFound();
        return Ok(MapVessel(v));
    }

    [HttpGet("imo/{imoNumber}")]
    public async Task<IActionResult> GetByImo(string imoNumber)
    {
        var v = await db.Vessels.FirstOrDefaultAsync(x => x.Imo == imoNumber && !x.IsDeleted);
        if (v == null) return NotFound();
        return Ok(MapVessel(v));
    }

    [HttpGet("mmsi/{mmsi}")]
    public async Task<IActionResult> GetByMmsi(string mmsi)
    {
        var v = await db.Vessels.FirstOrDefaultAsync(x => x.Mmsi == mmsi && !x.IsDeleted);
        if (v == null) return NotFound();
        return Ok(MapVessel(v));
    }

    [HttpPost("{id:guid}/refresh-position")]
    public async Task<IActionResult> RefreshPosition(Guid id)
    {
        var v = await db.Vessels.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (v == null) return NotFound();

        if (string.IsNullOrEmpty(v.Imo) && string.IsNullOrEmpty(v.Mmsi))
            return BadRequest(new { error = "Vessel has no IMO or MMSI to query AIS." });

        try
        {
            var result = string.IsNullOrEmpty(v.Mmsi)
                ? await ais.GetVesselPositionByImoAsync(v.Imo)
                : await ais.GetVesselPositionByMmsiAsync(v.Mmsi);

            if (result == null)
                return Ok(new { updated = false, message = "No AIS data found." });

            v.CurrentPosition = new Point(result.Longitude, result.Latitude) { SRID = 4326 };
            v.SpeedKnots = result.SpeedKnots;
            v.Heading = result.Heading;
            v.NavigationStatus = result.NavigationStatus;
            v.LastPositionUpdate = result.Timestamp;
            v.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok(new { updated = true, latitude = result.Latitude, longitude = result.Longitude });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = "AIS provider error.", detail = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireLogisticsManager")]
    public async Task<IActionResult> Create([FromBody] CreateVesselRequest req)
    {
        if (await db.Vessels.AnyAsync(v => v.Imo == req.Imo && !v.IsDeleted))
            return Conflict(new { error = "A vessel with this IMO number already exists." });

        var vessel = new Vessel
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Imo = req.Imo,
            Mmsi = req.Mmsi,
            Flag = req.Flag,
            Type = req.Type,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Vessels.Add(vessel);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = vessel.Id }, MapVessel(vessel));
    }

    private static object MapVessel(Vessel v) => new
    {
        v.Id, v.Name, v.Imo, v.Mmsi, v.Flag, v.Type,
        v.SpeedKnots, v.Heading, v.NavigationStatus,
        Latitude = v.CurrentPosition?.Y,
        Longitude = v.CurrentPosition?.X,
        v.LastPositionUpdate, v.CreatedAt, v.UpdatedAt
    };

    public record CreateVesselRequest(string Name, string Imo, string? Mmsi, string? Flag, string? Type);
}
