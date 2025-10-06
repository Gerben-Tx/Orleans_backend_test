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

    public async Task PlayerMovementUpdate(string groupName, string playerId, int posX, int posY) {
        Logger.LogDebug($"PlayerMovementUpdate received: {groupName}, {playerId}, ({posX},{posY})");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerMovementUpdate(playerId, posX, posY);
    }

    public async Task PlayerAddedToChunk(
        string groupName,
        string playerId,
        string playerName,
        int posX,
        int posY
    ) {
        Logger.LogDebug($"PlayerAddedToChunk received: {groupName}, {playerId}, {playerName}, ({posX},{posY})");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName)
            .PlayerAddedToChunk(playerId, playerName, posX, posY);
    }

    public async Task PlayerRemovedFromChunk(string groupName, string playerId) {
        Logger.LogDebug($"PlayerRemovedFromChunk received: {groupName}, {playerId}");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerRemovedFromChunk(playerId);
    }

    public async Task AddToGroupAsync(string groupName, string connectionId) {
        Logger.LogDebug($"AddToGroupAsync received: {groupName}, {connectionId}");

        await _realtimeUpdatesHubClientContext.Groups.AddToGroupAsync(connectionId, groupName);
    }

    public async Task RemoveFromGroupAsync(string groupName, string connectionId) {
        Logger.LogDebug($"RemoveFromGroupAsync received: {groupName}, {connectionId}");

        await _realtimeUpdatesHubClientContext.Groups.RemoveFromGroupAsync(connectionId, groupName);
    }
}