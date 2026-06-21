using ContainerTracking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ContainerTracking.Api.Middleware;

/// <summary>
/// Resolves the active organization for the request from JWT claims or X-Organization-Id header.
/// Validates that the user actually belongs to the claimed organization.
/// Platform admins can impersonate any org via header.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isPlatformAdmin = context.User.IsInRole("PlatformAdmin");

            var orgIdHeader = context.Request.Headers["X-Organization-Id"].FirstOrDefault();
            var orgIdClaim = context.User.FindFirst("org_id")?.Value;

            string? resolvedOrgId = null;

            if (isPlatformAdmin && !string.IsNullOrEmpty(orgIdHeader))
            {
                resolvedOrgId = orgIdHeader;
            }
            else if (!string.IsNullOrEmpty(orgIdClaim))
            {
                if (!string.IsNullOrEmpty(orgIdHeader) && orgIdHeader != orgIdClaim)
                {
                    if (userId != null && Guid.TryParse(orgIdHeader, out var reqOrgId))
                    {
                        var belongs = await db.UserOrganizations.AnyAsync(
                            uo => uo.UserId.ToString() == userId &&
                                  uo.OrganizationId == reqOrgId &&
                                  uo.IsActive);

                        if (belongs) resolvedOrgId = orgIdHeader;
                        else resolvedOrgId = orgIdClaim;
                    }
                    else resolvedOrgId = orgIdClaim;
                }
                else resolvedOrgId = orgIdClaim;
            }

            if (!string.IsNullOrEmpty(resolvedOrgId))
            {
                var identity = (ClaimsIdentity)context.User.Identity!;
                var existing = identity.FindFirst("resolved_org_id");
                if (existing != null) identity.RemoveClaim(existing);
                identity.AddClaim(new Claim("resolved_org_id", resolvedOrgId));
            }
        }

        await _next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
