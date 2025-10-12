using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;

namespace Backend.SignalR.Classes;

public class RealtimeUpdatesHubClient : RealtimeUpdatesHub<IRealtimeUpdatesClient>, IRealtimeUpdatesHub {
    public RealtimeUpdatesHubClient(
        IClusterClient orleansClient,
        ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>> logger
    ) : base(orleansClient, logger) { }

    public async Task RegisterPlayerGrain(string playerName) {
        Logger.LogDebug(
            "RegisterPlayerGrain received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        Context.Items["PlayerName"] = playerName;

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            Logger.LogDebug(
                "No existing player grain found for player name '{PlayerName}' in PlayerRegistry. Creating new one.",
                playerName);

            Guid newPlayerGuid = Guid.NewGuid();
            IPlayerRegistry playerRegistry = OrleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
            await playerRegistry.AddPlayer(playerName, newPlayerGuid);
            playerGrain = OrleansClient.GetGrain<IPlayerGrain>(newPlayerGuid);
        } else {
            Logger.LogDebug(
                "Existing player grain found for player name '{PlayerName}' in PlayerRegistry.",
                playerName);
        }

        await playerGrain.Initialize(Context.ConnectionId, playerName);

        Logger.LogDebug("Done registering player grain");
    }

    public async Task<long?> GetCurrentChunkId(string playerName) {
        Logger.LogDebug(
            "GetCurrentChunk received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return null;
        }

        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();

        return await currentChunk.GetKey();
    }

    public async Task MoveToChunk(string playerName, int newChunkId) {
        Logger.LogDebug(
            "MoveToChunk received from '{ContextConnectionId}': {PlayerName}, {NewChunkId}",
            Context.ConnectionId,
            playerName,
            newChunkId);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return;
        }

        IWorldChunkGrain newChunkGrain = OrleansClient.GetGrain<IWorldChunkGrain>(newChunkId);
        await playerGrain.EnterChunk(newChunkGrain);
    }

    public async Task<List<PlayerListMessage>> GetPlayersInCurrentChunk(string playerName) {
        Logger.LogDebug(
            "GetPlayersInCurrentChunk received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return [];
        }

        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();
        List<IPlayerGrain> playersInChunk = await currentChunk.GetAllPlayers();

        List<PlayerListMessage> messages = [];
        foreach (IPlayerGrain player in playersInChunk) {
            SerializableVector2 position = await player.GetPosition();
            messages.Add(
                new PlayerListMessage {
                    Id = await player.GetKey(),
                    Name = await player.GetName(),
                    PositionX = position.X,
                    PositionY = position.Y
                });
        }

        return messages;
    }

    private async Task<IPlayerGrain?> FindPlayerInRegistry(string playerName) {
        IPlayerRegistry playerRegistry = OrleansClient.GetGrain<IPlayerRegistry>(Guid.Empty);
        IPlayerGrain? existingPlayerGrain = await playerRegistry.FindPlayerByName(playerName);
        if (existingPlayerGrain == null) {
            Logger.LogError(
                "Could not find player grain for player name '{PlayerName}' in the PlayerRegistry",
                playerName);
            return null;
        }

        return existingPlayerGrain;
    }
}