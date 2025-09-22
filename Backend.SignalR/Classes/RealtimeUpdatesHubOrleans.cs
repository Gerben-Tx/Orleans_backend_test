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
    
    public async Task PlayerMovementUpdate(string groupName, string playerName, int posX, int posY) {
        Logger.LogDebug($"PlayerMovementUpdate received: {groupName}, {playerName}, ({posX},{posY})");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerMovementUpdate(playerName, posX, posY);
    }

    public async Task PlayerAddedToChunk(string groupName, string playerName) {
        Logger.LogDebug($"PlayerAddedToChunk received: {groupName}, {playerName}");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerAddedToChunk(playerName);
    }

    public async Task PlayerRemovedFromChunk(string groupName, string playerName) {
        Logger.LogDebug($"PlayerRemovedFromChunk received: {groupName}, {playerName}");

        await _realtimeUpdatesHubClientContext.Clients.Group(groupName).PlayerRemovedFromChunk(playerName);
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