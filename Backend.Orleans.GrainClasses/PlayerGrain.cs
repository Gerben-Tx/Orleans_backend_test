using System.Numerics;
using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;
using Roy_T.AStar.Graphs;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.GrainClasses;

public class PlayerGrain : BaseGrain, IPlayerGrain {
    private readonly IPersistentState<PlayerState> _playerState;
    private readonly ILogger<PlayerGrain> _logger;
    private string? _realtimeUpdatesConnectionId;
    private readonly IRealtimeUpdatesOrleans _realtimeUpdates;
    private readonly IPathfindingService _pathFindingService;
    private readonly Queue<SerializableVector2> _path = new();

    public PlayerGrain(
        [PersistentState("player", "tableStore")]
        IPersistentState<PlayerState> playerState,
        ILogger<PlayerGrain> logger,
        IRealtimeUpdatesOrleans realtimeUpdates,
        IPathfindingService pathfindingService
    ) : base(logger) {
        _playerState = playerState;
        _logger = logger;
        _realtimeUpdates = realtimeUpdates;
        _pathFindingService = pathfindingService;
    }

    public override async Task OnActivateAsync(
        CancellationToken cancellationToken
    ) {
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken
    ) {
        await LeaveChunk(await GetCurrentChunk());

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task EnterChunk(
        IWorldChunkGrain targetChunk
    ) {
        // List<string> playersInChunk = await chunk.GetAllPlayers();
        // string playerName = await GetPlayerName();
        // if (playersInChunk.Find(x => x == playerName) != null) {
        //     // Player is already in the new chunk, do nothing
        //     return;
        // }
        bool isPlayerInChunk = await targetChunk.IsPlayerInChunk(await GetKey());
        if (!isPlayerInChunk) {
            IWorldChunkGrain currentChunk = await GetCurrentChunk();

            _logger.LogDebug(
                "Moving player from chunk {CurrentChunkId} to chunk {NewChunkId}",
                currentChunk.GetKey(),
                targetChunk.GetKey()
            );

            // Exit from the current chunk
            await LeaveChunk(currentChunk);

            // Enter the new chunk
            await targetChunk.AddPlayer(this.GetPrimaryKeyString(), await GetName(), _playerState.State.Position);

            // Join realtime updates group for this chunk
            string chunkGroupName = await targetChunk.GetRealtimeUpdatesGroupName();
            await JoinRealtimeUpdatesGroup(chunkGroupName);
            
            // Join realtime updates group for neighboring chunks
            WorldChunkNeighbors? neighbors = await targetChunk.GetNeighboringChunks();
            foreach (WorldChunkNeighbor? neighbor in neighbors?.ToArray() ?? Array.Empty<WorldChunkNeighbor>()) {
                if (neighbor == null) {
                    continue;
                }
                
                await JoinRealtimeUpdatesGroup(
                    await GrainFactory.GetGrain<IWorldChunkGrain>(neighbor.Id).GetRealtimeUpdatesGroupName()
                );
            }
        }

        // Update new chunk id in state
        if (_playerState.State.ChunkGrain != targetChunk) {
            _playerState.State.ChunkGrain = targetChunk;
            await _playerState.WriteStateAsync();
        }
    }

    public async Task LeaveChunk(
        IWorldChunkGrain chunk
    ) {
        // Leave the chunk
        await chunk.RemovePlayer(this.GetPrimaryKeyString(), await GetName());

        // Leave the realtime updates group for this chunk
        string chunkGroupName = await chunk.GetRealtimeUpdatesGroupName();
        await LeaveRealtimeUpdatesGroup(chunkGroupName);
        
        // Leave realtime updates group for neighboring chunks
        WorldChunkNeighbors? neighbors = await chunk.GetNeighboringChunks();
        foreach (WorldChunkNeighbor? neighbor in neighbors?.ToArray() ?? Array.Empty<WorldChunkNeighbor>()) {
            if (neighbor == null) {
                continue;
            }
                
            await LeaveRealtimeUpdatesGroup(
                await GrainFactory.GetGrain<IWorldChunkGrain>(neighbor.Id).GetRealtimeUpdatesGroupName()
            );
        }
        
        // Persist state
        // Mainly for persisting position, which we don't need to do every tick.
        await _playerState.WriteStateAsync();
    }

    public Task<IWorldChunkGrain> GetCurrentChunk() {
        return Task.FromResult(_playerState.State.ChunkGrain ?? GrainFactory.GetGrain<IWorldChunkGrain>(0L));
    }

    public async Task Initialize(
        string connectionId,
        string playerName
    ) {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // The Name property is only null before this function. After it, it is always set.
        // We don't want to mark it nullable in PlayerState, because then we'd need to
        // always check for nulls which will never really happen.
        if (_playerState.State.Name == null) {
            _playerState.State.Name = playerName;
            await _playerState.WriteStateAsync();
        }

        _realtimeUpdatesConnectionId = connectionId;

        // Move player to his last known chunk
        await EnterChunk(await GetCurrentChunk());
        await StartMovementTimer();
    }

    public Task<SerializableVector2> GetPosition() {
        return Task.FromResult(_playerState.State.Position);
    }

    public Task<string> GetName() {
        return Task.FromResult(_playerState.State.Name);
    }

    public new Task DeactivateOnIdle() {
        base.DeactivateOnIdle();

        return Task.CompletedTask;
    }

    public Task<string> GetKey() => Task.FromResult(this.GetPrimaryKeyString());

    public async Task JoinRealtimeUpdatesGroup(
        string groupName
    ) {
        if (_realtimeUpdatesConnectionId == null) {
            return;
        }

        await _realtimeUpdates.AddToGroupAsync(groupName, _realtimeUpdatesConnectionId);
    }

    public async Task LeaveRealtimeUpdatesGroup(
        string groupName
    ) {
        if (_realtimeUpdatesConnectionId == null) {
            return;
        }

        await _realtimeUpdates.RemoveFromGroupAsync(groupName, _realtimeUpdatesConnectionId);
    }

    /// <summary>
    /// Starts a timer that sends random movement updates to the client.
    /// This is just for testing purposes. Eventually, the client should be able to choose its own movement.
    /// </summary>
    /// <returns></returns>
    public Task StartMovementTimer() {
        this.RegisterGrainTimer(
            // SendRandomMovementUpdate,
            SendPathMovementUpdate,
            new GrainTimerCreationOptions(
                TimeSpan.FromSeconds(0),
                TimeSpan.FromMilliseconds(250)
            ) { Interleave = true, KeepAlive = true }
        );

        return Task.CompletedTask;
    }

    private async Task SendPathMovementUpdate() {
        if (_path.Count == 0) {
            _logger.LogDebug("No path found, creating new path...");
            Random rand = new();
            Path? path = await _pathFindingService.FindPath(
                _playerState.State.Position.ToVector2(),
                new Vector2(
                    // TODO: For now we use random coords, but we should eventually get the destination from the client
                    rand.Next(0, WorldChunkGrain.SizeX - 1),
                    rand.Next(0, WorldChunkGrain.SizeY - 1)
                )
            );
            if (path is null) {
                _logger.LogWarning("Could not find a path!");
                return;
            }
        
            foreach (IEdge? edge in path.Edges) {
                _path.Enqueue(new SerializableVector2((int)edge.End.Position.X, (int)edge.End.Position.Y));
            }
        }
        
        // If we somehow have no path, just return
        if (_path.Count == 0) {
            _logger.LogWarning("Path is empty!");
            return;
        }
        
        _logger.LogDebug("Sending path movement update...");
        
        SerializableVector2 newPosition = _path.Dequeue();
        _playerState.State.Position = newPosition;

        IWorldChunkGrain currentChunk = await GetCurrentChunk();
        WorldChunkGrainPosition? position = await currentChunk.GetPositionByChunkId();
        if (position == null) {
            _logger.LogWarning("Could not find position for chunk {ChunkId}!", currentChunk.GetKey());
            return;
        }

        await _realtimeUpdates.PlayerMovementUpdate(
            await currentChunk.GetRealtimeUpdatesGroupName(),
            this.GetPrimaryKeyString(),
            newPosition.X + (position.X * WorldChunkGrain.SizeX),
            newPosition.Y + (position.Y * WorldChunkGrain.SizeY)
        );
    }
}