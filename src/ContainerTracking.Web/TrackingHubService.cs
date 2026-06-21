using Blazored.LocalStorage;
using ContainerTracking.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace ContainerTracking.Web;

public class TrackingHubService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly string _apiBase;
    private readonly ILocalStorageService _storage;

    public event Action<ContainerUpdateMessage>? ContainerUpdated;
    public event Action<AlertNotificationMessage>? AlertReceived;
    public event Action<VesselPositionMessage>? VesselPositionUpdated;
    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public TrackingHubService(string apiBase, ILocalStorageService storage)
    {
        _apiBase = apiBase;
        _storage = storage;
    }

    public async Task StartAsync()
    {
        if (_hub?.State == HubConnectionState.Connected) return;

        var token = await _storage.GetItemAsStringAsync("auth_token");

        _hub = new HubConnectionBuilder()
            .WithUrl($"{_apiBase}/hubs/tracking", opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)])
            .Build();

        _hub.On<ContainerUpdateMessage>("ContainerUpdate", msg => ContainerUpdated?.Invoke(msg));
        _hub.On<AlertNotificationMessage>("AlertNotification", msg => AlertReceived?.Invoke(msg));
        _hub.On<VesselPositionMessage>("VesselPosition", msg => VesselPositionUpdated?.Invoke(msg));
        _hub.On<DashboardRefreshMessage>("DashboardRefresh", _ => { });

        _hub.Reconnected += async _ =>
        {
            Console.WriteLine("SignalR reconnected");
            await Task.CompletedTask;
        };

        await _hub.StartAsync();
    }

    public async Task SubscribeToContainerAsync(Guid containerId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.SendAsync("SubscribeToContainer", containerId);
    }

    public async Task UnsubscribeFromContainerAsync(Guid containerId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.SendAsync("UnsubscribeFromContainer", containerId);
    }

    public async Task StopAsync()
    {
        if (_hub != null) await _hub.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null) await _hub.DisposeAsync();
    }
}
