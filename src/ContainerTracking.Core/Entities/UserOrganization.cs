using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class UserOrganization : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Role { get; set; } = Roles.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime? InvitedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public string? InvitedByUserId { get; set; }
}
