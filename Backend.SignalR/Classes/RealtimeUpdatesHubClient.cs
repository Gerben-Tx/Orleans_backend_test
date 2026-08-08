using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Orleans.Concurrency;

namespace Backend.SignalR.Classes;

public class RealtimeUpdatesHubClient : RealtimeUpdatesHub<IRealtimeUpdatesClient>, IRealtimeUpdatesHub {
    public RealtimeUpdatesHubClient(
        IClusterClient orleansClient,
        ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>> logger
    ) : base(orleansClient, logger) { }

    public async Task<string> RegisterPlayerGrain(
        string playerName
    ) {
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

        return await playerGrain.GetKey();
    }

    public async Task<WorldChunkContract> GetCurrentChunk(
        string playerName
    ) {
        Logger.LogDebug(
            "GetCurrentChunk received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return null;
        }

        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();
        WorldChunkGrainPosition? position = await currentChunk.GetPosition();
        if (position == null) {
            return null;
        }

        return new WorldChunkContract(
            await currentChunk.GetKey(),
            position.X,
            position.Y
        );
    }

    public async Task DebugMoveToChunk(
        string playerName,
        int newChunkId
    ) {
        Logger.LogDebug(
            "DebugMoveToChunk received from '{ContextConnectionId}': {PlayerName}, {NewChunkId}",
            Context.ConnectionId,
            playerName,
            newChunkId);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return;
        }

        IWorldChunkGrain newChunkGrain = OrleansClient.GetGrain<IWorldChunkGrain>(newChunkId);
        await playerGrain.DebugMoveToChunk(newChunkGrain);
    }

    public async Task<List<PlayerListMessage>> GetPlayersInChunk(
        string playerName,
        long chunkId
    ) {
        Logger.LogDebug(
            "GetPlayersInChunk received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return [];
        }

        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();
        List<IPlayerGrain> playersInChunk = [];
        if (chunkId == await currentChunk.GetKey()) {
            playersInChunk = await currentChunk.GetAllPlayers();
        } else {
            VisibleWorldChunk[] visibleChunks =
                await currentChunk.GetVisibleChunks(await playerGrain.GetChunkVisibilityRadius());
            VisibleWorldChunk? matchingVisibleChunk =
                visibleChunks.ToArray().FirstOrDefault(visibleChunk => visibleChunk?.Id == chunkId);

            if (matchingVisibleChunk != null) {
                IWorldChunkGrain visibleChunkGrain = OrleansClient.GetGrain<IWorldChunkGrain>(matchingVisibleChunk.Id);
                playersInChunk = await visibleChunkGrain.GetAllPlayers();
            }
        }


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

    public async Task<VisibleWorldChunksMessage> GetVisibleChunks(
        string playerName,
        int radius
    ) {
        Logger.LogDebug(
            "GetVisibleChunks received from '{ContextConnectionId}': {PlayerName}",
            Context.ConnectionId,
            playerName);

        IPlayerGrain? playerGrain = await FindPlayerInRegistry(playerName);
        if (playerGrain == null) {
            return new VisibleWorldChunksMessage([]);
        }

        playerGrain.SetChunkVisibilityRadius(radius);

        IWorldChunkGrain currentChunkGrain = await playerGrain.GetCurrentChunk();
        VisibleWorldChunk[] visibleWorldChunks = await currentChunkGrain.GetVisibleChunks(radius);

        return new VisibleWorldChunksMessage(
            visibleWorldChunks.Select(visibleWorldChunk =>
                    new WorldChunkContract(
                        visibleWorldChunk.Id,
                        visibleWorldChunk.Position.X,
                        visibleWorldChunk.Position.Y)
                )
                .ToArray()
        );
    }

    public async Task<WorldInfoMessage> GetWorldInfo() {
        ITickGrain tickGrain = OrleansClient.GetGrain<ITickGrain>(ITickGrain.Key);

        return new WorldInfoMessage(
            IWorldChunkGrain.WorldSizeX,
            IWorldChunkGrain.WorldSizeY,
            IWorldChunkGrain.SizeX,
            IWorldChunkGrain.SizeY,
            await tickGrain.GetTicks(),
            await tickGrain.GetTicksPerSecond()
        );
    }

    private async Task<IPlayerGrain?> FindPlayerInRegistry(
        string playerName
    ) {
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