namespace ContainerTracking.Core.Enums;

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string LogisticsManager = "LogisticsManager";
    public const string Viewer = "Viewer";
    public const string ApiClient = "ApiClient";
}

public static class Policies
{
    public const string RequirePlatformAdmin = "RequirePlatformAdmin";
    public const string RequireOrganizationAdmin = "RequireOrganizationAdmin";
    public const string RequireLogisticsManager = "RequireLogisticsManager";
    public const string RequireViewer = "RequireViewer";
    public const string RequireOrganizationAccess = "RequireOrganizationAccess";
}
