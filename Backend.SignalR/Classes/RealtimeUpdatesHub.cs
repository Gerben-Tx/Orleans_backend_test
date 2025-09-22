using Microsoft.AspNetCore.SignalR;

namespace Backend.SignalR.Classes;

public class RealtimeUpdatesHub<T> : Hub<T> where T : class {
    protected readonly IClusterClient OrleansClient;
    protected readonly ILogger<RealtimeUpdatesHub<T>> Logger;

    protected RealtimeUpdatesHub(
        IClusterClient orleansClient,
        ILogger<RealtimeUpdatesHub<T>> logger
    ) {
        OrleansClient = orleansClient;
        Logger = logger;
    }

    public override Task OnDisconnectedAsync(Exception? exception) {
        // TODO: Get player grain by it's connection id (we don't have the player name here..)
        // TODO: Make sure the player grain stops

        return base.OnDisconnectedAsync(exception);
    }

    // TODO: make different hubs for different types of updates
}