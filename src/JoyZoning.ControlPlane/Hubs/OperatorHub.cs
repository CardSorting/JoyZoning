using Microsoft.AspNetCore.SignalR;

namespace JoyZoning.ControlPlane.Hubs;

public class OperatorHub : Hub
{
    public Task SubscribeSession(string sessionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

    public Task UnsubscribeSession(string sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
}
