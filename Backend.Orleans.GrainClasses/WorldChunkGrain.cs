using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;
using WorldChunkNeighbor = Backend.Orleans.SharedContracts.WorldChunkNeighbor;

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
            await GetKey(),
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

        await _realtimeUpdates.PlayerRemovedFromChunk(_groupName, playerGrainKey, await GetKey());
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

    public async Task<WorldChunkGrainPosition?> GetPosition() {
        long chunkId = await GetKey();
        return await GetPositionByChunkId(chunkId);
    }

    public async Task<WorldChunkGrainPosition?> GetPositionByChunkId(
        long chunkId
    ) {
        int x = (int)(chunkId % WorldSizeX);
        int y = (int)((chunkId - x) / WorldSizeX);

        bool withinBounds = await IsPositionWithinBounds(new WorldChunkGrainPosition(x, y));
        return withinBounds ? new WorldChunkGrainPosition(x, y) : null;
    }

    public async Task<long?> GetChunkIdByPosition(
        WorldChunkGrainPosition position
    ) {
        bool withinBounds = await IsPositionWithinBounds(position);
        return withinBounds ? position.Y * WorldSizeX + position.X : null;
    }

    private static Task<bool> IsPositionWithinBounds(
        WorldChunkGrainPosition position
    ) {
        return Task.FromResult(
            position.X >= 0
            && position.X < WorldSizeX
            && position.Y >= 0
            && position.Y < WorldSizeY
        );
    }

    public async Task<WorldChunkNeighbor[]> GetNeighboringChunksById(
        long? chunkId = null
    ) {
        if (chunkId == null) {
            chunkId = await GetKey();
        }

        WorldChunkGrainPosition? pos = await GetPositionByChunkId(chunkId.Value);
        if (pos == null) {
            _logger.LogWarning("No position found for chunk id: {chunkId}", chunkId);
            return [];
        }

        async Task<WorldChunkNeighbor?> GetNeighbor(
            int dx,
            int dy
        ) {
            WorldChunkGrainPosition nPos = new(pos.X + dx, pos.Y + dy);
            long? nId = await GetChunkIdByPosition(nPos);
            return nId.HasValue ? new WorldChunkNeighbor(nId.Value, nPos) : null;
        }

        List<WorldChunkNeighbor> ret = [];
        (int X, int Y)[] offsets = [
            (0, -1),
            (1, -1),
            (1, 0),
            (1, 1),
            (0, 1),
            (-1, 1),
            (-1, 0),
            (-1, -1),
        ];
        foreach ((int X, int Y) offset in offsets) {
            WorldChunkNeighbor? chunkNeighbor = await GetNeighbor(offset.X, offset.Y);
            if (chunkNeighbor == null) {
                continue;
            }

            ret.Add(chunkNeighbor);
        }

        return ret.ToArray();
    }

    public async Task<WorldChunkNeighbor[]> GetNeighboringChunks(
        int radius = 1
    ) {
        // Use Breadth-First Search (BFS) to find all neighboring chunks within the specified radius.
        // This ensures we discover all reachable chunks layer by layer (radius 1, then radius 2, etc.).

        // Track visited chunk IDs to avoid redundant processing and prevent infinite loops 
        // caused by back-references between neighboring chunks. 
        // We initialize it with the current chunk ID so it's excluded from the results.
        long currentChunkId = await GetKey();
        HashSet<long> visited = [currentChunkId];
        List<WorldChunkNeighbor> allNeighbors = [];
        List<long> currentLayer = [currentChunkId];

        for (int i = 0; i < radius; i++) {
            // Fetch neighbors for all chunks in the current layer in parallel.
            // This significantly reduces total latency compared to sequential requests,
            // especially when the radius is large or network latency is involved.
            WorldChunkNeighbor[][] results =
                await Task.WhenAll(currentLayer.Select(id => GetNeighboringChunksById(id)));

            List<long> nextLayer = [];
            foreach (WorldChunkNeighbor[] neighbors in results) {
                foreach (WorldChunkNeighbor neighbor in neighbors) {
                    // Only process and return chunks we haven't seen yet in previous layers.
                    if (!visited.Add(neighbor.Id)) continue;

                    allNeighbors.Add(neighbor);
                    nextLayer.Add(neighbor.Id);
                }
            }

            currentLayer = nextLayer;
            if (currentLayer.Count == 0) {
                // No more neighbors found, can stop early.
                break;
            }
        }

        return allNeighbors.ToArray();
    }
}