using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.AspNetCore.SignalR;

namespace Backend.SignalR.Classes;

public class RealtimeUpdatesHub : Hub {
    private readonly IClusterClient _orleansClient;
    private readonly ILogger<RealtimeUpdatesHub> _logger;

    public RealtimeUpdatesHub(
        IClusterClient orleansClient,
        ILogger<RealtimeUpdatesHub> logger
    ) {
        _orleansClient = orleansClient;
        _logger = logger;
    }

    public Task Debug(string message) {
        _logger.LogDebug("Debug message received from '{ContextConnectionId}': {Message}", Context.ConnectionId,
            message);
        return Task.CompletedTask;
    }

    public async Task RegisterPlayerGrain(string playerName) {
        _logger.LogDebug("RegisterPlayerGrain received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId, playerName);

        IPlayerRegistry playerRegistry = _orleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        Guid? existingPlayerGuid = await playerRegistry.GetPlayer(playerName);
        Guid playerGuid = existingPlayerGuid ?? Guid.NewGuid();
        if (existingPlayerGuid == null) {
            _logger.LogDebug(
                "No existing guid found for player name '{PlayerName}' in PlayerRegistry. Creating new one.",
                playerName);
            await playerRegistry.AddPlayer(playerName, playerGuid);
        } else {
            _logger.LogDebug("Existing guid found for player name '{PlayerName}' in PlayerRegistry.", playerName);
        }

        IPlayerGrain playerGrain = _orleansClient.GetGrain<IPlayerGrain>(playerGuid);
        await playerGrain.Initialize(Context.ConnectionId);

        _logger.LogDebug("Done registering player grain");
    }

    public async Task<long?> GetCurrentChunkId(string playerName) {
        _logger.LogDebug("GetCurrentChunk received from '{ContextConnectionId}': {PlayerName}", Context.ConnectionId,
            playerName);

        IPlayerRegistry playerRegistry = _orleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        Guid? existingPlayerGuid = await playerRegistry.GetPlayer(playerName);
        if (existingPlayerGuid == null) {
            _logger.LogError(
                "Could not find player guid for player name '{PlayerName}' in the PlayerRegistry", playerName);
            return null;
        }

        IPlayerGrain playerGrain = _orleansClient.GetGrain<IPlayerGrain>(existingPlayerGuid.Value);
        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();

        return currentChunk.GetPrimaryKeyLong();
    }

    public async Task MoveToChunk(string playerName, int newChunkId) {
        _logger.LogDebug("MoveToChunk received from '{ContextConnectionId}': {PlayerName}, {NewChunkId}",
            Context.ConnectionId, playerName, newChunkId);

        IPlayerRegistry playerRegistry = _orleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        Guid? existingPlayerGuid = await playerRegistry.GetPlayer(playerName);
        if (existingPlayerGuid == null) {
            _logger.LogError("Could not find player guid for player name '{PlayerName}' in the PlayerRegistry",
                playerName);
            return;
        }

        IPlayerGrain playerGrain = _orleansClient.GetGrain<IPlayerGrain>(existingPlayerGuid.Value);
        IWorldChunkGrain newChunkGrain = _orleansClient.GetGrain<IWorldChunkGrain>(newChunkId);
        await playerGrain.EnterChunk(newChunkGrain);
    }

    public async Task<List<PlayerListMessage>> GetPlayersInCurrentChunk(string playerName) {
        _logger.LogDebug("GetPlayersInCurrentChunk received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId, playerName);

        IPlayerRegistry playerRegistry = _orleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        Guid? existingPlayerGuid = await playerRegistry.GetPlayer(playerName);
        if (existingPlayerGuid == null) {
            _logger.LogError("Could not find player guid for player name '{PlayerName}' in the PlayerRegistry",
                playerName);
            return [];
        }

        IPlayerGrain playerGrain = _orleansClient.GetGrain<IPlayerGrain>(existingPlayerGuid.Value);
        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();
        List<IPlayerGrain> playersInChunk = await currentChunk.GetAllPlayers();

        List<PlayerListMessage> messages = [];
        foreach (IPlayerGrain player in playersInChunk) {
            string name = player.GetPrimaryKeyString();
            SerializableVector2 position = await player.GetPosition();
            messages.Add(new PlayerListMessage { Name = name, PositionX = position.X, PositionY = position.Y });
        }

        return messages;
    }
}