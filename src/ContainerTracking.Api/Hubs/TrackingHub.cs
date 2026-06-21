using ContainerTracking.Core.Enums;
using ContainerTracking.Core.Interfaces;
using ContainerTracking.Core.Models;
using ContainerTracking.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Api.Hubs;

[Authorize]
public class TrackingHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ITierEnforcementService _tierService;
    private readonly ILogger<TrackingHub> _logger;

    public TrackingHub(AppDbContext db, ITierEnforcementService tierService, ILogger<TrackingHub> logger)
    {
        _db = db;
        _tierService = tierService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var orgId = GetOrganizationId();
        if (orgId == null)
        {
            _logger.LogWarning("Hub connection rejected: missing org_id claim for {ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var tierCheck = await _tierService.CanUseWebSocketAsync(orgId.Value);
        if (!tierCheck.Allowed)
        {
            _logger.LogWarning("Hub connection rejected for org {OrgId}: {Reason}", orgId, tierCheck.DenialReason);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, OrgGroup(orgId.Value));

        if (IsPlatformAdmin())
            await Groups.AddToGroupAsync(Context.ConnectionId, "platform-admin");

        _logger.LogInformation("Client {ConnectionId} connected to org group {OrgId}", Context.ConnectionId, orgId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var orgId = GetOrganizationId();
        if (orgId != null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrgGroup(orgId.Value));

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SubscribeToContainer(Guid containerId)
    {
        var orgId = GetOrganizationId();
        if (orgId == null) return;

        var exists = await _db.Containers.AnyAsync(c => c.Id == containerId && c.OrganizationId == orgId);
        if (!exists) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, ContainerGroup(containerId));
        _logger.LogDebug("Client {ConnectionId} subscribed to container {ContainerId}", Context.ConnectionId, containerId);
    }

    public async Task UnsubscribeFromContainer(Guid containerId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ContainerGroup(containerId));
    }

    public async Task SubscribeToShipment(Guid shipmentId)
    {
        var orgId = GetOrganizationId();
        if (orgId == null) return;

        var exists = await _db.Shipments.AnyAsync(s => s.Id == shipmentId && s.OrganizationId == orgId);
        if (!exists) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, ShipmentGroup(shipmentId));
    }

    public async Task RequestDashboardRefresh()
    {
        var orgId = GetOrganizationId();
        if (orgId == null) return;

        await Clients.Caller.SendAsync("DashboardRefresh", new DashboardRefreshMessage
        {
            OrganizationId = orgId.Value,
            RefreshType = "full"
        });
    }

    public static string OrgGroup(Guid orgId) => $"org-{orgId}";
    public static string ContainerGroup(Guid containerId) => $"container-{containerId}";
    public static string ShipmentGroup(Guid shipmentId) => $"shipment-{shipmentId}";

    private Guid? GetOrganizationId()
    {
        var claim = Context.User?.FindFirst("org_id")?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsPlatformAdmin() =>
        Context.User?.IsInRole(Roles.PlatformAdmin) ?? false;
}

public class SignalRPublisher : ISignalRPublisher
{
    private readonly IHubContext<TrackingHub> _hub;

    public SignalRPublisher(IHubContext<TrackingHub> hub)
    {
        _hub = hub;
    }

    public async Task PublishContainerUpdateAsync(ContainerUpdateMessage message, CancellationToken ct = default)
    {
        await _hub.Clients.Group(TrackingHub.OrgGroup(message.OrganizationId))
            .SendAsync("ContainerUpdate", message, ct);

        await _hub.Clients.Group(TrackingHub.ContainerGroup(message.ContainerId))
            .SendAsync("ContainerUpdate", message, ct);
    }

    public async Task PublishAlertAsync(AlertNotificationMessage message, CancellationToken ct = default)
    {
        await _hub.Clients.Group(TrackingHub.OrgGroup(message.OrganizationId))
            .SendAsync("AlertNotification", message, ct);
    }

    public async Task PublishVesselPositionAsync(VesselPositionMessage message, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("VesselPosition", message, ct);
    }
}
