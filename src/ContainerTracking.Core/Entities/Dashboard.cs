using ContainerTracking.Core.Enums;

namespace ContainerTracking.Core.Entities;

public class DashboardLayout : OrganizationScopedEntity
{
    public string Name { get; set; } = "Default Dashboard";
    public bool IsDefault { get; set; } = false;
    public bool IsOrganizationDefault { get; set; } = false;
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public int Columns { get; set; } = 12;
    public string? Theme { get; set; }
    public bool IsPublic { get; set; } = false;

    public ICollection<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
}

public class DashboardWidget : BaseEntity
{
    public Guid DashboardLayoutId { get; set; }
    public DashboardLayout DashboardLayout { get; set; } = null!;
    public WidgetType WidgetType { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Column { get; set; } = 0;
    public int Row { get; set; } = 0;
    public int Width { get; set; } = 4;
    public int Height { get; set; } = 4;
    public bool IsVisible { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 60;
    public Dictionary<string, object> Config { get; set; } = new();
}
