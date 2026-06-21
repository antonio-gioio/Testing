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
public class ShipmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentOrganizationContext _orgContext;

    public ShipmentsController(AppDbContext db, ITierEnforcementService tierService,
        IAuditLogService auditLog, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _auditLog = auditLog;
        _orgContext = orgContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetShipments(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] ShipmentStatus? status = null, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var query = _db.Shipments
            .Include(s => s.Vessel)
            .Include(s => s.Containers)
            .Where(s => s.OrganizationId == orgId);

        if (status.HasValue) query = query.Where(s => s.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new ShipmentSummaryDto(s))
            .ToListAsync(ct);

        return Ok(new PagedResult<ShipmentSummaryDto>(items, total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetShipment(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var shipment = await _db.Shipments
            .Include(s => s.Vessel)
            .Include(s => s.Containers)
            .Include(s => s.BillsOfLading)
            .Include(s => s.TrackingEvents)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);

        if (shipment == null) return NotFound();
        return Ok(shipment);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();

        var shipment = new Shipment
        {
            OrganizationId = orgId,
            Reference = request.Reference,
            Description = request.Description,
            CarrierName = request.CarrierName,
            OriginPort = request.OriginPort,
            OriginPortCode = request.OriginPortCode,
            DestinationPort = request.DestinationPort,
            DestinationPortCode = request.DestinationPortCode,
            EstimatedDeparture = request.EstimatedDeparture,
            EstimatedArrival = request.EstimatedArrival,
            VoyageNumber = request.VoyageNumber,
            Incoterms = request.Incoterms,
            Status = ShipmentStatus.Draft
        };

        _db.Shipments.Add(shipment);
        await _db.SaveChangesAsync(ct);
        await _auditLog.LogAsync("Create", "Shipment", shipment.Id, orgId, GetUserId(), newValues: request, ct: ct);
        return CreatedAtAction(nameof(GetShipment), new { id = shipment.Id }, shipment);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> UpdateShipment(Guid id, [FromBody] CreateShipmentRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (shipment == null) return NotFound();

        shipment.Description = request.Description;
        shipment.CarrierName = request.CarrierName;
        shipment.OriginPort = request.OriginPort;
        shipment.DestinationPort = request.DestinationPort;
        shipment.EstimatedDeparture = request.EstimatedDeparture;
        shipment.EstimatedArrival = request.EstimatedArrival;

        await _db.SaveChangesAsync(ct);
        return Ok(shipment);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> DeleteShipment(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (shipment == null) return NotFound();
        shipment.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>POST /api/v1/shipments/{id}/import-csv — import containers via CSV</summary>
    [HttpPost("{id:guid}/import-csv")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> ImportCsv(Guid id, IFormFile file, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var limits = await _tierService.GetLimitsAsync(orgId, ct);
        if (!limits.CsvImportEnabled)
            return StatusCode(402, new { error = "CSV import requires Starter tier or higher." });

        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (shipment == null) return NotFound();

        if (file.Length == 0) return BadRequest("File is empty.");
        if (file.ContentType != "text/csv" && !file.FileName.EndsWith(".csv"))
            return BadRequest("Only CSV files are accepted.");
        if (file.Length > 5_000_000) return BadRequest("File too large (max 5MB).");

        using var reader = new StreamReader(file.OpenReadStream());
        var imported = 0;
        string? line;
        bool header = true;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (header) { header = false; continue; }
            var parts = line.Split(',');
            if (parts.Length < 1) continue;
            var containerNum = parts[0].Trim().Trim('"').ToUpperInvariant();
            if (string.IsNullOrEmpty(containerNum)) continue;

            if (!await _db.Containers.AnyAsync(c => c.OrganizationId == orgId && c.ContainerNumber == containerNum, ct))
            {
                _db.Containers.Add(new Container
                {
                    OrganizationId = orgId,
                    ContainerNumber = containerNum,
                    ShipmentId = id,
                    IsTrackingActive = true
                });
                imported++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { imported, message = $"Imported {imported} containers." });
    }

    [HttpGet("{id:guid}/containers")]
    public async Task<IActionResult> GetContainers(Guid id, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var exists = await _db.Shipments.AnyAsync(s => s.Id == id && s.OrganizationId == orgId, ct);
        if (!exists) return NotFound();

        var containers = await _db.Containers
            .Where(c => c.ShipmentId == id && c.OrganizationId == orgId && !c.IsDeleted)
            .Select(c => new
            {
                c.Id, c.ContainerNumber, Status = c.Status.ToString(),
                c.SizeType, c.LastEventAt
            })
            .OrderBy(c => c.ContainerNumber)
            .ToListAsync(ct);

        return Ok(containers);
    }

    private Guid GetOrgId()
    {
        var id = _orgContext.OrganizationId;
        if (!id.HasValue) throw new UnauthorizedAccessException("Organization context required");
        return id.Value;
    }

    private Guid? GetUserId()
    {
        var id = _orgContext.UserId;
        return id != null && Guid.TryParse(id, out var g) ? g : null;
    }
}

public record ShipmentSummaryDto(
    Guid Id, string Reference, string Status, string? OriginPort,
    string? DestinationPort, DateTime? EstimatedArrival, int ContainerCount)
{
    public ShipmentSummaryDto(Shipment s) : this(
        s.Id, s.Reference, s.Status.ToString(), s.OriginPort,
        s.DestinationPort, s.EstimatedArrival, s.Containers.Count) { }
}

public record CreateShipmentRequest(
    string Reference, string? Description, string? CarrierName,
    string? OriginPort, string? OriginPortCode, string? DestinationPort, string? DestinationPortCode,
    DateTime? EstimatedDeparture, DateTime? EstimatedArrival,
    string? VoyageNumber, string? Incoterms);
