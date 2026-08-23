using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class WorldChunkGrain : BaseGrain, IWorldChunkGrain {
    private readonly List<string> _players = [];
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
        SerializableVector2 playerPosition,
        Queue<SerializableVector2> path
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
            playerPosition.Y,
            path.ToList().ConvertAll<int[]>(x => x.ToArray()).ToArray() // TODO: move this converting to a function
            // Added the path to this update
            // because when a player enters a new chunk,
            // the other players will never receive the PlayerNewPathCreated
            // update because the path was already created.
            // And they need the path if they want to see the player move
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
        int x = (int)(chunkId % IWorldChunkGrain.WorldSizeX);
        int y = (int)((chunkId - x) / IWorldChunkGrain.WorldSizeX);

        bool withinBounds = await IsPositionWithinBounds(new WorldChunkGrainPosition(x, y));
        return withinBounds ? new WorldChunkGrainPosition(x, y) : null;
    }

    public async Task<long?> GetChunkIdByPosition(
        WorldChunkGrainPosition position
    ) {
        bool withinBounds = await IsPositionWithinBounds(position);
        return withinBounds ? position.Y * IWorldChunkGrain.WorldSizeX + position.X : null;
    }

    private static Task<bool> IsPositionWithinBounds(
        WorldChunkGrainPosition position
    ) {
        return Task.FromResult(
            position.X >= 0
            && position.X < IWorldChunkGrain.WorldSizeX
            && position.Y >= 0
            && position.Y < IWorldChunkGrain.WorldSizeY
        );
    }

    public async Task<VisibleWorldChunk[]> GetVisibleChunksById(
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

        async Task<VisibleWorldChunk?> GetVisibleChunk(
            int dx,
            int dy
        ) {
            WorldChunkGrainPosition nPos = new(pos.X + dx, pos.Y + dy);
            long? nId = await GetChunkIdByPosition(nPos);
            return nId.HasValue ? new VisibleWorldChunk(nId.Value, nPos) : null;
        }

        List<VisibleWorldChunk> ret = [];
        (int X, int Y)[] offsets = [
            (0, -1),
            (1, -1),
            (1, 0),
            (0, 0),
            (1, 1),
            (0, 1),
            (-1, 1),
            (-1, 0),
            (-1, -1),
        ];
        foreach ((int X, int Y) offset in offsets) {
            VisibleWorldChunk? visibleChunk = await GetVisibleChunk(offset.X, offset.Y);
            if (visibleChunk == null) {
                continue;
            }

            ret.Add(visibleChunk);
        }

        return ret.ToArray();
    }

    public async Task<VisibleWorldChunk[]> GetVisibleChunks(
        int radius = 1
    ) {
        // Use Breadth-First Search (BFS) to find all chunks within the specified radius.
        // This ensures we discover all reachable chunks layer by layer (radius 1, then radius 2, etc.).

        // Track visited chunk IDs to avoid redundant processing and prevent infinite loops 
        // caused by back-references between visible chunks. 
        // We initialize it with the current chunk ID so it's excluded from the results.
        long currentChunkId = await GetKey();
        HashSet<long> visited = [];
        List<VisibleWorldChunk> allVisibleChunks = [];
        List<long> currentLayer = [currentChunkId];

        for (int i = 0; i < radius; i++) {
            // Fetch visible chunks for all chunks in the current layer in parallel.
            // This significantly reduces total latency compared to sequential requests,
            // especially when the radius is large or network latency is involved.
            VisibleWorldChunk[][] results =
                await Task.WhenAll(currentLayer.Select(id => GetVisibleChunksById(id)));

            List<long> nextLayer = [];
            foreach (VisibleWorldChunk[] visibleChunks in results) {
                foreach (VisibleWorldChunk visibleChunk in visibleChunks) {
                    // Only process and return chunks we haven't seen yet in previous layers.
                    if (!visited.Add(visibleChunk.Id)) continue;

                    allVisibleChunks.Add(visibleChunk);
                    nextLayer.Add(visibleChunk.Id);
                }
            }

            currentLayer = nextLayer;
            if (currentLayer.Count == 0) {
                // No more visible chunks found, can stop early.
                break;
            }
        }

        return allVisibleChunks.ToArray();
    }
}