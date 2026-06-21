using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/bills-of-lading")]
[Authorize]
public class BillsOfLadingController(AppDbContext db, ICurrentOrganizationContext ctx) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var orgId = ctx.OrganizationId;
        var query = db.BillsOfLading.Where(b => b.OrganizationId == orgId && !b.IsDeleted);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(b => b.BolNumber.Contains(search) || b.ShipperName!.Contains(search) || b.ConsigneeName!.Contains(search));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id, b.BolNumber, b.CarrierName, b.ShipperName, b.ConsigneeName,
                b.OriginPort, b.DestinationPort, b.IssueDate, b.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, total, page, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var orgId = ctx.OrganizationId;
        var bol = await db.BillsOfLading
            .Include(b => b.TrackingEvents.Where(e => !e.IsDeleted).OrderByDescending(e => e.OccurredAt).Take(50))
            .FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId && !b.IsDeleted);
        if (bol == null) return NotFound();

        return Ok(new
        {
            bol.Id, bol.BolNumber, bol.CarrierName, bol.ShipperName, bol.ConsigneeName,
            bol.NotifyPartyName, bol.OriginPortCode, bol.OriginPort,
            bol.DestinationPortCode, bol.DestinationPort, bol.IssueDate,
            bol.CreatedAt, bol.UpdatedAt,
            events = bol.TrackingEvents.Select(e => new
            {
                e.Id, EventType = e.EventType.ToString(), e.Description, e.Location, e.OccurredAt, ProviderType = e.ProviderType.ToString()
            })
        });
    }

    [HttpGet("number/{bolNumber}")]
    public async Task<IActionResult> GetByNumber(string bolNumber)
    {
        var orgId = ctx.OrganizationId;
        var bol = await db.BillsOfLading.FirstOrDefaultAsync(b => b.BolNumber == bolNumber && b.OrganizationId == orgId && !b.IsDeleted);
        if (bol == null) return NotFound();
        return RedirectToAction(nameof(Get), new { id = bol.Id });
    }

    [HttpPost]
    [Authorize(Policy = "RequireLogisticsManager")]
    public async Task<IActionResult> Create([FromBody] CreateBolRequest req)
    {
        var orgId = ctx.OrganizationId;
        if (await db.BillsOfLading.AnyAsync(b => b.BolNumber == req.BolNumber && b.OrganizationId == orgId && !b.IsDeleted))
            return Conflict(new { error = "A B/L with this number already exists." });

        var bol = new BillOfLading
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BolNumber = req.BolNumber,
            CarrierName = req.CarrierName,
            ShipperName = req.ShipperName,
            ConsigneeName = req.ConsigneeName,
            NotifyPartyName = req.NotifyPartyName,
            OriginPort = req.OriginPort,
            OriginPortCode = req.OriginPortCode,
            DestinationPort = req.DestinationPort,
            DestinationPortCode = req.DestinationPortCode,
            IssueDate = req.IssueDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.BillsOfLading.Add(bol);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = bol.Id }, new { bol.Id, bol.BolNumber });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireLogisticsManager")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var orgId = ctx.OrganizationId;
        var bol = await db.BillsOfLading.FirstOrDefaultAsync(b => b.Id == id && b.OrganizationId == orgId && !b.IsDeleted);
        if (bol == null) return NotFound();
        bol.IsDeleted = true;
        bol.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record CreateBolRequest(
        string BolNumber, string? CarrierName, string? ShipperName, string? ConsigneeName,
        string? NotifyPartyName, string? OriginPort, string? OriginPortCode,
        string? DestinationPort, string? DestinationPortCode, DateTime? IssueDate);
}
