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
public class OrganizationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly IAuditLogService _auditLog;
    private readonly ICurrentOrganizationContext _orgContext;

    public OrganizationsController(AppDbContext db, ITierEnforcementService tierService,
        IAuditLogService auditLog, ICurrentOrganizationContext orgContext)
    {
        _db = db;
        _tierService = tierService;
        _auditLog = auditLog;
        _orgContext = orgContext;
    }

    /// <summary>GET /api/v1/organizations — platform admins only, list all orgs</summary>
    [HttpGet]
    [Authorize(Roles = Roles.PlatformAdmin)]
    public async Task<IActionResult> GetAllOrganizations(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var query = _db.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.SubscriptionTier)
            .Include(o => o.UsageCounter)
            .Where(o => !o.IsDeleted);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(o => o.Name.Contains(search) || o.Slug.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(o => o.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrgSummaryDto(o))
            .ToListAsync(ct);

        return Ok(new PagedResult<OrgSummaryDto>(items, total, page, pageSize));
    }

    /// <summary>GET /api/v1/organizations/mine — get current organization details</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMyOrganization(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var org = await _db.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.SubscriptionTier)
            .Include(o => o.UsageCounter)
            .FirstOrDefaultAsync(o => o.Id == orgId, ct);

        if (org == null) return NotFound();
        return Ok(org);
    }

    /// <summary>PUT /api/v1/organizations/mine — update organization settings</summary>
    [HttpPut("mine")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> UpdateOrganization([FromBody] UpdateOrgRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == orgId, ct);
        if (org == null) return NotFound();

        org.Name = request.Name ?? org.Name;
        org.ContactEmail = request.ContactEmail ?? org.ContactEmail;
        org.WebsiteUrl = request.WebsiteUrl ?? org.WebsiteUrl;
        org.PrimaryColor = request.PrimaryColor ?? org.PrimaryColor;
        org.Country = request.Country ?? org.Country;

        await _db.SaveChangesAsync(ct);
        await _auditLog.LogAsync("Update", "Organization", orgId, orgId, GetUserId(), ct: ct);
        return Ok(org);
    }

    /// <summary>GET /api/v1/organizations/mine/users — list org users</summary>
    [HttpGet("mine/users")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> GetOrgUsers(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var users = await _db.UserOrganizations
            .Include(uo => uo.User)
            .Where(uo => uo.OrganizationId == orgId && uo.IsActive)
            .Select(uo => new
            {
                userId = uo.UserId,
                email = uo.User.Email,
                firstName = uo.User.FirstName,
                lastName = uo.User.LastName,
                role = uo.Role,
                joinedAt = uo.AcceptedAt
            })
            .ToListAsync(ct);

        return Ok(users);
    }

    /// <summary>POST /api/v1/organizations/mine/users/{userId}/role — change a user's role</summary>
    [HttpPut("mine/users/{userId:guid}/role")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> ChangeUserRole(Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var userOrg = await _db.UserOrganizations
            .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationId == orgId, ct);

        if (userOrg == null) return NotFound();

        var validRoles = new[] { Roles.OrganizationAdmin, Roles.LogisticsManager, Roles.Viewer };
        if (!validRoles.Contains(request.Role))
            return BadRequest(new { error = "Invalid role. Use OrganizationAdmin, LogisticsManager, or Viewer." });

        userOrg.Role = request.Role;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// <summary>DELETE /api/v1/organizations/mine/users/{userId} — remove user from org</summary>
    [HttpDelete("mine/users/{userId:guid}")]
    [Authorize(Roles = $"{Roles.OrganizationAdmin},{Roles.PlatformAdmin}")]
    public async Task<IActionResult> RemoveUser(Guid userId, CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var userOrg = await _db.UserOrganizations
            .FirstOrDefaultAsync(uo => uo.UserId == userId && uo.OrganizationId == orgId, ct);

        if (userOrg == null) return NotFound();
        userOrg.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>GET /api/v1/organizations/mine/tier — tier limits and usage</summary>
    [HttpGet("mine/tier")]
    public async Task<IActionResult> GetTierInfo(CancellationToken ct = default)
    {
        var orgId = GetOrgId();
        var usage = await _tierService.GetUsageSummaryAsync(orgId, ct);
        var limits = await _tierService.GetLimitsAsync(orgId, ct);
        return Ok(new { usage, limits });
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

public record OrgSummaryDto(Guid Id, string Name, string Slug, bool IsActive, string? TierName, int? ActiveContainers)
{
    public OrgSummaryDto(Organization o) : this(
        o.Id, o.Name, o.Slug, o.IsActive,
        o.Subscription?.SubscriptionTier?.Name,
        o.UsageCounter?.ActiveContainers) { }
}

public record UpdateOrgRequest(string? Name, string? ContactEmail, string? WebsiteUrl, string? PrimaryColor, string? Country);
public record ChangeRoleRequest(string Role);
