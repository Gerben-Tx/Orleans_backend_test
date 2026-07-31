using Backend.SignalR.SharedContracts;
using Microsoft.AspNetCore.SignalR;

namespace Backend.SignalR.Classes;

public class RealtimeUpdatesHubOrleans : RealtimeUpdatesHub<IRealtimeUpdatesClient>, IRealtimeUpdatesOrleans {
    private readonly IHubContext<RealtimeUpdatesHubClient, IRealtimeUpdatesClient> _realtimeUpdatesHubClientContext;

    public RealtimeUpdatesHubOrleans(
        IClusterClient orleansClient,
        ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>> logger,
        IHubContext<RealtimeUpdatesHubClient, IRealtimeUpdatesClient> realtimeUpdatesHubClientContext
    ) : base(orleansClient, logger) {
        _realtimeUpdatesHubClientContext = realtimeUpdatesHubClientContext;
    }

    public async Task PlayerNewPathCreated(string groupName, string playerId, int[][] path) {
        Logger.LogDebug($"PlayerNewPathCreated received: {groupName}, {playerId}, {path})");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerNewPathCreated(playerId, path);
    }

    public async Task PlayerAddedToChunk(
        string groupName,
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        int[][] path
    ) {
        Logger.LogDebug($"PlayerAddedToChunk received: {groupName}, {playerId}, {playerName}, {chunkId}, ({posX},{posY}), {path}");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName)
            .PlayerAddedToChunk(playerId, playerName, chunkId, posX, posY, path);
    }

    public async Task PlayerRemovedFromChunk(string groupName, string playerId, long chunkId) {
        Logger.LogDebug($"PlayerRemovedFromChunk received: {groupName}, {playerId}");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerRemovedFromChunk(playerId, chunkId);
    }

    public async Task AddToGroupAsync(string groupName, string connectionId) {
        Logger.LogDebug($"AddToGroupAsync received: {groupName}, {connectionId}");

        await _realtimeUpdatesHubClientContext.Groups.AddToGroupAsync(connectionId, groupName);
    }

    public async Task RemoveFromGroupAsync(string groupName, string connectionId) {
        Logger.LogDebug($"RemoveFromGroupAsync received: {groupName}, {connectionId}");

        await _realtimeUpdatesHubClientContext.Groups.RemoveFromGroupAsync(connectionId, groupName);
    }

    public async Task Tick(string groupName) {
        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).Tick();
    }
}