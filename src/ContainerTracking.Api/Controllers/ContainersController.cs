using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ContainersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentOrganizationContext _orgContext;

    public ContainersController(AppDbContext db, ITierEnforcementService tierService,
        IAuditLogService auditLog, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _auditLog = auditLog;
        _orgContext = orgContext;
    }

    /// <summary>GET /api/v1/containers — list containers for current organization</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContainerDto>), 200)]
    public async Task<IActionResult> GetContainers(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] ContainerStatus? status = null, [FromQuery] string? search = null,
        [FromQuery] Guid? shipmentId = null, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var query = _db.Containers
            .Include(c => c.Shipment)
            .Where(c => c.OrganizationId == orgId && !c.IsDeleted);

        if (status.HasValue) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrEmpty(search)) query = query.Where(c => c.ContainerNumber.Contains(search));
        if (shipmentId.HasValue) query = query.Where(c => c.ShipmentId == shipmentId);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.LastEventAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => MapToDto(c)).ToListAsync(ct);

        return Ok(new PagedResult<ContainerDto>(items, total, page, pageSize));
    }

    /// <summary>GET /api/v1/containers/{id} — get container detail with tracking history</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContainerDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetContainer(Guid id, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var container = await _db.Containers
            .Include(c => c.Shipment)
            .Include(c => c.BillOfLading)
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId, ct);

        if (container == null) return NotFound();

        var events = await _db.TrackingEvents
            .Where(e => e.ContainerId == id)
            .OrderByDescending(e => e.EventTime)
            .Take(100)
            .ToListAsync(ct);

        return Ok(new ContainerDetailDto(container, events));
    }

    /// <summary>POST /api/v1/containers — add new container to track</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    [ProducesResponseType(typeof(ContainerDto), 201)]
    public async Task<IActionResult> CreateContainer([FromBody] CreateContainerRequest request, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();

        var tierCheck = await _tierService.CanAddContainerAsync(orgId, ct);
        if (!tierCheck.Allowed)
            return StatusCode(402, new { error = tierCheck.DenialReason, current = tierCheck.CurrentUsage, limit = tierCheck.Limit });

        if (await _db.Containers.AnyAsync(c => c.OrganizationId == orgId && c.ContainerNumber == request.ContainerNumber.ToUpperInvariant(), ct))
            return Conflict(new { error = "Container number already tracked in this organization." });

        var container = new Container
        {
            OrganizationId = orgId,
            ContainerNumber = request.ContainerNumber.ToUpperInvariant(),
            SizeType = request.SizeType,
            CargoDescription = request.CargoDescription,
            ShipmentId = request.ShipmentId,
            IsTrackingActive = true,
            PublicTrackingToken = request.EnablePublicLink ? GenerateToken() : null
        };

        _db.Containers.Add(container);

        await UpdateUsageCounter(orgId, delta: +1, field: "containers", ct);
        await _db.SaveChangesAsync(ct);
        await _auditLog.LogAsync("Create", "Container", container.Id, orgId, GetUserId(), newValues: request, ct: ct);

        return CreatedAtAction(nameof(GetContainer), new { id = container.Id }, MapToDto(container));
    }

    /// <summary>PATCH /api/v1/containers/{id} — update container fields</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> UpdateContainer(Guid id, [FromBody] UpdateContainerRequest request, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var container = await _db.Containers.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId, ct);
        if (container == null) return NotFound();

        if (request.SizeType != null) container.SizeType = request.SizeType;
        if (request.CargoDescription != null) container.CargoDescription = request.CargoDescription;
        if (request.ShipmentId.HasValue) container.ShipmentId = request.ShipmentId;
        if (request.IsTrackingActive.HasValue) container.IsTrackingActive = request.IsTrackingActive.Value;

        await _db.SaveChangesAsync(ct);
        await _auditLog.LogAsync("Update", "Container", container.Id, orgId, GetUserId(), ct: ct);
        return Ok(MapToDto(container));
    }

    /// <summary>DELETE /api/v1/containers/{id} — soft delete container</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.LogisticsManager}")]
    public async Task<IActionResult> DeleteContainer(Guid id, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var container = await _db.Containers.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId, ct);
        if (container == null) return NotFound();

        container.IsDeleted = true;
        container.IsTrackingActive = false;
        await UpdateUsageCounter(orgId, delta: -1, field: "containers", ct);
        await _db.SaveChangesAsync(ct);
        await _auditLog.LogAsync("Delete", "Container", container.Id, orgId, GetUserId(), ct: ct);
        return NoContent();
    }

    /// <summary>GET /api/v1/containers/{id}/events — paginated tracking event history</summary>
    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> GetContainerEvents(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var exists = await _db.Containers.AnyAsync(c => c.Id == id && c.OrganizationId == orgId, ct);
        if (!exists) return NotFound();

        var query = _db.TrackingEvents.Where(e => e.ContainerId == id);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.EventTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<TrackingEvent>(items, total, page, pageSize));
    }

    /// <summary>GET /track/{token} — public tracking link (no auth required)</summary>
    [HttpGet("/track/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicTrack(string token, CancellationToken ct = default)
    {
        var container = await _db.Containers
            .Include(c => c.Shipment)
            .FirstOrDefaultAsync(c => c.PublicTrackingToken == token, ct);

        if (container == null) return NotFound();

        var events = await _db.TrackingEvents
            .Where(e => e.ContainerId == container.Id)
            .OrderByDescending(e => e.EventTime).Take(20)
            .ToListAsync(ct);

        return Ok(new
        {
            containerNumber = container.ContainerNumber,
            status = container.Status.ToString(),
            currentLocation = container.CurrentLocation,
            eta = container.EtaDestination,
            lastUpdated = container.LastEventAt,
            events = events.Select(e => new
            {
                time = e.EventTime,
                description = e.Description,
                location = e.Location,
                type = e.EventType.ToString()
            })
        });
    }

    [HttpGet("{id:guid}/map-summary")]
    public async Task<IActionResult> GetMapSummary(Guid id, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var container = await _db.Containers
            .Include(c => c.Shipment)
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId && !c.IsDeleted, ct);
        if (container == null) return NotFound();

        return Ok(new
        {
            container.Id, container.ContainerNumber,
            Status = container.Status.ToString(),
            ShipmentReference = container.Shipment?.Reference,
            container.LastEventAt,
            Latitude = container.CurrentPosition?.Y,
            Longitude = container.CurrentPosition?.X
        });
    }

    /// <summary>POST /api/v1/containers/{id}/poll — immediately poll all providers for latest events</summary>
    [HttpPost("{id:guid}/poll")]
    [Authorize(Policy = "RequireLogisticsManager")]
    public async Task<IActionResult> PollContainer(
        Guid id,
        [FromServices] IContainerPollingService pollService,
        CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var exists = await _db.Containers.AnyAsync(c => c.Id == id && c.OrganizationId == orgId && !c.IsDeleted, ct);
        if (!exists) return NotFound();

        var newEvents = await pollService.PollContainerNowAsync(id, orgId, ct);
        return Ok(new { newEvents, message = $"Poll complete. {newEvents} new event(s) found." });
    }

    [HttpPost("{id:guid}/events")]
    [Authorize(Policy = "RequireLogisticsManager")]
    public async Task<IActionResult> AddManualEvent(Guid id, [FromBody] ManualEventRequest req, CancellationToken ct = default)
    {
        var orgId = RequireOrgId();
        var container = await _db.Containers.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId && !c.IsDeleted, ct);
        if (container == null) return NotFound();

        if (!Enum.TryParse<Core.Enums.TrackingEventType>(req.EventType, out var eventType))
            return BadRequest(new { error = "Invalid eventType value." });

        var newStatus = MapEventTypeToStatus(eventType);
        var evt = new TrackingEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            ContainerId = id,
            EventType = eventType,
            ProviderType = Core.Enums.TrackingProviderType.Manual,
            Description = req.Description ?? eventType.ToString(),
            Location = req.Location,
            EventTime = req.EventTime ?? DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ContainerStatusAfter = newStatus
        };
        _db.TrackingEvents.Add(evt);
        container.LastEventAt = evt.EventTime;
        if (newStatus.HasValue)
        {
            container.Status = newStatus.Value;
            if (req.Location != null) container.CurrentLocation = req.Location;
        }
        await _db.SaveChangesAsync(ct);
        return Created($"/api/v1/tracking-events/{evt.Id}", new { evt.Id, newStatus = newStatus?.ToString() });
    }

    private Guid RequireOrgId()
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

    private async Task UpdateUsageCounter(Guid orgId, int delta, string field, CancellationToken ct)
    {
        var counter = await _db.TierUsageCounters.FirstOrDefaultAsync(u => u.OrganizationId == orgId, ct);
        if (counter == null) return;
        if (field == "containers") counter.ActiveContainers = Math.Max(0, counter.ActiveContainers + delta);
        await _db.SaveChangesAsync(ct);
    }

    private static ContainerStatus? MapEventTypeToStatus(TrackingEventType type) => type switch
    {
        TrackingEventType.ContainerGateIn => ContainerStatus.GateIn,
        TrackingEventType.ContainerLoaded => ContainerStatus.Loaded,
        TrackingEventType.VesselDeparted => ContainerStatus.Departed,
        TrackingEventType.VesselArrived => ContainerStatus.Arrived,
        TrackingEventType.ContainerDischarged => ContainerStatus.Discharged,
        TrackingEventType.ContainerGateOut => ContainerStatus.GateOut,
        TrackingEventType.ContainerDelivered => ContainerStatus.Delivered,
        TrackingEventType.TransshipmentArrived => ContainerStatus.TransshipmentArrived,
        TrackingEventType.TransshipmentDeparted => ContainerStatus.TransshipmentDeparted,
        TrackingEventType.BookingConfirmed => ContainerStatus.BookingConfirmed,
        TrackingEventType.EmptyReturned => ContainerStatus.Empty,
        _ => null
    };

    private static string GenerateToken()
    {
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static ContainerDto MapToDto(Container c) => new(c);
}

public record ContainerDto(
    Guid Id, string ContainerNumber, string Status, string? CurrentLocation,
    DateTime? EtaDestination, DateTime? LastEventAt, string? ShipmentReference,
    bool HasPublicLink)
{
    public ContainerDto(Container c) : this(
        c.Id, c.ContainerNumber, c.Status.ToString(), c.CurrentLocation,
        c.EtaDestination, c.LastEventAt, c.Shipment?.Reference,
        c.PublicTrackingToken != null) { }
}

public record ContainerDetailDto(Container Container, List<TrackingEvent> Events);

public record CreateContainerRequest(
    string ContainerNumber, string? SizeType, string? CargoDescription,
    Guid? ShipmentId, bool EnablePublicLink = false);

public record UpdateContainerRequest(
    string? SizeType, string? CargoDescription, Guid? ShipmentId, bool? IsTrackingActive);

public record ManualEventRequest(string EventType, string? Description, string? Location, DateTime? EventTime);

public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
