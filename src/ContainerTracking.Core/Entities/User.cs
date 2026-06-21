using Microsoft.AspNetCore.Identity;

namespace ContainerTracking.Core.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? TimeZone { get; set; } = "UTC";
    public string? Language { get; set; } = "en";
    public bool IsPlatformAdmin { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
    public ICollection<DashboardLayout> DashboardLayouts { get; set; } = new List<DashboardLayout>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
