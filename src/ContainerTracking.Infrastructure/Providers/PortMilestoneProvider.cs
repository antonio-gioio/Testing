using ContainerTracking.Core.Entities;
using ContainerTracking.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ContainerTracking.Infrastructure.Providers;

/// <summary>
/// Stub for port community system APIs (e.g. PortBase, Portcast, Portchain).
/// Concrete carrier implementations override this with real HTTP calls.
/// </summary>
public class PortMilestoneProvider(ILogger<PortMilestoneProvider> logger) : IPortMilestoneProvider
{
    public Task<bool> CheckHealthAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<IEnumerable<NormalizedTrackingEvent>> GetContainerEventsAsync(Container container, ProviderCredential? credential, CancellationToken ct = default)
    {
        logger.LogDebug("PortMilestoneProvider: no config for container {Number}", container.ContainerNumber);
        return Task.FromResult(Enumerable.Empty<NormalizedTrackingEvent>());
    }

    public Task<IEnumerable<NormalizedTrackingEvent>> GetBolEventsAsync(BillOfLading bol, ProviderCredential? credential, CancellationToken ct = default)
    {
        logger.LogDebug("PortMilestoneProvider: no config for BOL {Number}", bol.BolNumber);
        return Task.FromResult(Enumerable.Empty<NormalizedTrackingEvent>());
    }

    public Task<IEnumerable<NormalizedTrackingEvent>> GetPortCallEventsAsync(string portCode, string? vesselImo = null, DateTime? from = null, CancellationToken ct = default)
    {
        logger.LogDebug("PortMilestoneProvider: no config for port {Code}", portCode);
        return Task.FromResult(Enumerable.Empty<NormalizedTrackingEvent>());
    }
}
