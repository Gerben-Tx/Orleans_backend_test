using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class WorldChunkGrain : BaseGrain, IWorldChunkGrain {
    private readonly List<string> _players = [];
    public const int SizeX = 30;
    public const int SizeY = 30;
    public const int WorldSizeX = 10; // In chunks
    public const int WorldSizeY = 10; // In chunks
    private readonly string _groupName;
    private readonly ILogger<WorldChunkGrain> _logger;
    private readonly IRealtimeUpdatesOrleans _realtimeUpdates;

    public WorldChunkGrain(
        ILogger<WorldChunkGrain> logger,
        IRealtimeUpdatesOrleans realtimeUpdates
    ) : base(logger) {
        _groupName = this.GetPrimaryKeyLong().ToString();
        _logger = logger;
        _realtimeUpdates = realtimeUpdates;
    }

    public async Task AddPlayer(
        string playerGrainKey,
        string playerName,
        SerializableVector2 playerPosition
    ) {
        if (_players.Contains(playerGrainKey)) {
            // Player already exists in this chunk
            return;
        }

        _players.Add(playerGrainKey);

        await _realtimeUpdates.PlayerAddedToChunk(
            _groupName,
            playerGrainKey,
            playerName,
            playerPosition.X,
            playerPosition.Y
        );
    }

    public async Task RemovePlayer(
        string playerGrainKey,
        string playerName
    ) {
        if (!_players.Contains(playerGrainKey)) {
            // Player doesn't exist in this chunk
            return;
        }

        _players.Remove(playerGrainKey);

        await _realtimeUpdates.PlayerRemovedFromChunk(_groupName, playerGrainKey);
    }

    public Task<string> GetRealtimeUpdatesGroupName() => Task.FromResult(_groupName);
    public Task<long> GetKey() => Task.FromResult(this.GetPrimaryKeyLong());

    public Task<bool> IsPlayerInChunk(
        string playerGrainKey
    ) {
        return Task.FromResult(_players.Contains(playerGrainKey));
    }

    public Task<List<IPlayerGrain>> GetAllPlayers() {
        List<IPlayerGrain> players = [];
        foreach (string playerKey in _players) {
            players.Add(this.GrainFactory.GetGrain<IPlayerGrain>(Guid.Parse(playerKey)));
        }

        return Task.FromResult(players);
    }

    public static WorldChunkGrainPosition? GetPositionByChunkId(
        long chunkId
    ) {
        int x = (int)(chunkId % WorldSizeX);
        int y = (int)((chunkId - x) / WorldSizeX);

        return IsPositionWithinBounds(new WorldChunkGrainPosition(x, y)) ? new WorldChunkGrainPosition(x, y) : null;
    }

    public static long? GetChunkIdByPosition(
        WorldChunkGrainPosition position
    ) {
        return IsPositionWithinBounds(position) ? position.Y * WorldSizeX + position.X : null;
    }

    public static bool IsPositionWithinBounds(
        WorldChunkGrainPosition position
    ) {
        if (position.X < 0 || position.X >= WorldSizeX) {
            return false;
        }

        if (position.Y < 0 || position.Y >= WorldSizeY) {
            return false;
        }

        return true;
    }

    public Task<WorldChunkNeighbors> GetNeighboringChunks(
        long chunkId
    ) {
        WorldChunkGrainPosition? pos = GetPositionByChunkId(chunkId);
        if (pos == null) {
            _logger.LogWarning("No position found for chunk id: {chunkId}", chunkId);
            return Task.FromResult(new WorldChunkNeighbors());
        }

        WorldChunkNeighbor? GetNeighbor(int dx, int dy) {
            WorldChunkGrainPosition nPos = new(pos.X + dx, pos.Y + dy);
            long? nId = GetChunkIdByPosition(nPos);
            return nId.HasValue ? new WorldChunkNeighbor(nId.Value, nPos) : null;
        }

        return Task.FromResult(new WorldChunkNeighbors(
            GetNeighbor(0, -1),
            GetNeighbor(1, -1),
            GetNeighbor(1, 0),
            GetNeighbor(1, 1),
            GetNeighbor(0, 1),
            GetNeighbor(-1, 1),
            GetNeighbor(-1, 0),
            GetNeighbor(-1, -1)
        ));
    }
}