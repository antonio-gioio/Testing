using ContainerTracking.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ContainerTracking.Infrastructure.Data;

public class CurrentOrganizationContext : ICurrentOrganizationContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentOrganizationContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OrganizationId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("org_id")?.Value;
            return claim != null && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsPlatformAdmin =>
        _httpContextAccessor.HttpContext?.User.IsInRole("PlatformAdmin") ?? false;
}
