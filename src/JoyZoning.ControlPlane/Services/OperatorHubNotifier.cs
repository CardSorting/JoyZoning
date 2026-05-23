using JoyZoning.ControlPlane.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Services;

/// <summary>Best-effort SignalR broadcasts — failures must not break orchestration.</summary>
public sealed class OperatorHubNotifier
{
    private readonly IHubContext<OperatorHub> _hub;
    private readonly ILogger<OperatorHubNotifier> _logger;

    public OperatorHubNotifier(IHubContext<OperatorHub> hub, ILogger<OperatorHubNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task BroadcastAsync(
        string method,
        object? payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients.All.SendAsync(method, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR broadcast {Method} skipped", method);
        }
    }
}
