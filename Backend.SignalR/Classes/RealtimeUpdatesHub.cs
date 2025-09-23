using Backend.Orleans.SharedContracts;
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

    public override async Task OnDisconnectedAsync(Exception? exception) {
        Logger.LogDebug($"OnDisconnectedAsync: {exception?.Message}");

        string? playerName = Context.Items["PlayerName"] as string;
        if (playerName == null) {
            throw new Exception("Player name not found in context items");
        }
        
        IPlayerRegistry playerRegistry = OrleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        IPlayerGrain? playerGrain = await playerRegistry.FindPlayerByName(playerName);
        if (playerGrain != null) {
            Logger.LogDebug("Connection disconnected, deactivating player grain...");
            await playerGrain.DeactivateOnIdle();
        }

        await base.OnDisconnectedAsync(exception);
    }
}