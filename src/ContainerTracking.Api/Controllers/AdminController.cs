using ContainerTracking.Core.Entities;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ContainerTracking.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "RequirePlatformAdmin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetSystemStats()
    {
        var totalOrgs = await db.Organizations.CountAsync(o => !o.IsDeleted && o.IsActive);
        var totalUsers = await db.Users.CountAsync(u => u.IsActive);
        var totalContainers = await db.Containers.CountAsync(c => !c.IsDeleted);
        var activeContainers = await db.Containers.CountAsync(c => !c.IsDeleted && c.IsTrackingActive);
        var totalShipments = await db.Shipments.CountAsync(s => !s.IsDeleted);
        var totalEvents = await db.TrackingEvents.CountAsync(e => !e.IsDeleted);
        var eventsToday = await db.TrackingEvents.CountAsync(e => !e.IsDeleted && e.CreatedAt >= DateTime.UtcNow.Date);

        return Ok(new
        {
            totalOrgs, totalUsers, totalContainers, activeContainers,
            totalShipments, totalEvents, eventsToday,
            asOf = DateTime.UtcNow
        });
    }

    [HttpGet("organizations")]
    public async Task<IActionResult> ListOrganizations([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null)
    {
        var query = db.Organizations.Where(o => !o.IsDeleted);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(o => o.Name.Contains(search) || o.Slug.Contains(search));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(o => o.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt,
                ContainerCount = db.Containers.Count(c => c.OrganizationId == o.Id && !c.IsDeleted),
                UserCount = db.UserOrganizations.Count(uo => uo.OrganizationId == o.Id && uo.IsActive)
            })
            .ToListAsync();

        return Ok(new { items, total, page, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    [HttpGet("organizations/{id:guid}")]
    public async Task<IActionResult> GetOrganization(Guid id)
    {
        var org = await db.Organizations
            .Include(o => o.Subscription).ThenInclude(s => s!.SubscriptionTier)
            .Include(o => o.UsageCounter)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        if (org == null) return NotFound();

        return Ok(new
        {
            org.Id, org.Name, org.Slug, org.IsActive, org.CreatedAt,
            subscription = org.Subscription == null ? null : new
            {
                org.Subscription.Id,
                TierName = org.Subscription.SubscriptionTier?.Name,
                org.Subscription.Status,
                org.Subscription.TrialEndsAt,
                org.Subscription.CurrentPeriodEnd
            },
            usage = org.UsageCounter == null ? null : new
            {
                org.UsageCounter.ActiveContainers,
                org.UsageCounter.ActiveUsers,
                org.UsageCounter.ApiCallsThisHour,
                org.UsageCounter.ApiCallsToday
            }
        });
    }

    [HttpPatch("organizations/{id:guid}/status")]
    public async Task<IActionResult> SetOrgStatus(Guid id, [FromBody] SetStatusRequest req)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        if (org == null) return NotFound();
        org.IsActive = req.IsActive;
        org.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { org.Id, org.IsActive });
    }

    [HttpGet("tiers")]
    public async Task<IActionResult> ListTiers()
    {
        var tiers = await db.SubscriptionTiers.OrderBy(t => t.SortOrder).ToListAsync();
        return Ok(tiers);
    }

    [HttpPut("tiers/{id:guid}")]
    public async Task<IActionResult> UpdateTier(Guid id, [FromBody] UpdateTierRequest req)
    {
        var tier = await db.SubscriptionTiers.FindAsync(id);
        if (tier == null) return NotFound();

        tier.MaxContainers = req.MaxContainers ?? tier.MaxContainers;
        tier.MaxUsers = req.MaxUsers ?? tier.MaxUsers;
        tier.MaxShipments = req.MaxShipments ?? tier.MaxShipments;
        tier.MaxAlerts = req.MaxAlerts ?? tier.MaxAlerts;
        tier.UpdateIntervalMinutes = req.UpdateIntervalMinutes ?? tier.UpdateIntervalMinutes;
        tier.MonthlyPriceUsd = req.MonthlyPriceUsd ?? tier.MonthlyPriceUsd;
        tier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(tier);
    }

    [HttpGet("providers")]
    public async Task<IActionResult> ListProviders()
    {
        var providers = await db.TrackingProviders.Where(p => !p.IsDeleted).OrderBy(p => p.Name).ToListAsync();
        return Ok(providers.Select(p => new { p.Id, p.Name, p.ProviderType, p.IsEnabled, p.UpdatedAt }));
    }

    [HttpPatch("providers/{id:guid}/status")]
    public async Task<IActionResult> SetProviderStatus(Guid id, [FromBody] SetStatusRequest req)
    {
        var provider = await db.TrackingProviders.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (provider == null) return NotFound();
        provider.IsEnabled = req.IsActive;
        provider.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { provider.Id, provider.IsEnabled });
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] Guid? orgId, [FromQuery] string? action,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = db.AuditLogs.AsQueryable();
        if (orgId.HasValue) query = query.Where(a => a.OrganizationId == orgId);
        if (!string.IsNullOrEmpty(action)) query = query.Where(a => a.Action == action);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { items, total, page, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    public record SetStatusRequest(bool IsActive);
    public record UpdateTierRequest(int? MaxContainers, int? MaxUsers, int? MaxShipments,
        int? MaxAlerts, int? UpdateIntervalMinutes, decimal? MonthlyPriceUsd);
}
