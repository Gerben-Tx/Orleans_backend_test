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
    private int _chunkVisibileRadius = 1;

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
        bool isPlayerInChunk = await targetChunk.IsPlayerInChunk(await GetKey());
        if (!isPlayerInChunk) {
            IWorldChunkGrain currentChunk = await GetCurrentChunk();

            _logger.LogDebug(
                "Moving player from chunk {CurrentChunkId} to chunk {NewChunkId}",
                await currentChunk.GetKey(),
                await targetChunk.GetKey()
            );

            // Exit from the current chunk
            await LeaveChunk(currentChunk);
            
            // Join realtime updates group for visible chunks
            List<Task> parallelizeTasks = [];
            VisibleWorldChunk[] visibleChunks = await targetChunk.GetVisibleChunks(_chunkVisibileRadius);
            foreach (VisibleWorldChunk visibleChunk in visibleChunks) {
                parallelizeTasks.Add(
                    JoinRealtimeUpdatesGroup(
                        await GrainFactory.GetGrain<IWorldChunkGrain>(visibleChunk.Id).GetRealtimeUpdatesGroupName()
                    ));
            }
            
            // Enter the new chunk
            // Must be below JoinRealtimeUpdatesGroup, otherwise the client won't receive this update
            await targetChunk.AddPlayer(this.GetPrimaryKeyString(), await GetName(), _playerState.State.Position);

            await Task.WhenAll(parallelizeTasks);
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

        // TODO: maybe its better if we leave only the chunks that become not-visible..
        // Leave realtime updates group for visible chunks
        List<Task> parallelizeTasks = [];
        VisibleWorldChunk?[] visibleChunks = await chunk.GetVisibleChunks(_chunkVisibileRadius);
        foreach (VisibleWorldChunk? visibleChunk in visibleChunks) {
            if (visibleChunk == null) {
                continue;
            }

            parallelizeTasks.Add(
                LeaveRealtimeUpdatesGroup(
                    await GrainFactory.GetGrain<IWorldChunkGrain>(visibleChunk.Id).GetRealtimeUpdatesGroupName()
                ));
        }

        await Task.WhenAll(parallelizeTasks);

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
    public Task<int> GetChunkVisibilityRadius() => Task.FromResult(_chunkVisibileRadius);

    public void SetChunkVisibilityRadius(
        int radius
    ) {
        _chunkVisibileRadius = radius;
    }

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
                    rand.Next(0, _pathFindingService.GetGrid().Columns),
                    rand.Next(0, _pathFindingService.GetGrid().Rows)
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

        // _logger.LogDebug("Sending path movement update...");
        SerializableVector2 newPosition = _path.Dequeue();
        // // Ugly hack to make the player move faster for DEBUGGING
        // // It skips multiple nodes in the path
        // for (int i = 0; i < 5; i++) {
        //     newPosition = _path.Dequeue();
        // }
        _playerState.State.Position = newPosition;

        IWorldChunkGrain currentChunk = await GetCurrentChunk();
        WorldChunkGrainPosition? currentChunkPosition = await currentChunk.GetPosition();
        if (currentChunkPosition == null) {
            _logger.LogWarning("Could not find position for chunk {ChunkId}!", currentChunk.GetKey());
            return;
        }

        var newChunkPosition = new WorldChunkGrainPosition(
            newPosition.X / WorldChunkGrain.SizeX,
            newPosition.Y / WorldChunkGrain.SizeY
        );
        
        // Enter new chunk if we cross borders
        if (newChunkPosition != currentChunkPosition) {
            _logger.LogDebug("Current chunk position: {CurrentChunkPosition} | New chunk position: {NewChunkPosition}", currentChunkPosition, newChunkPosition);
            
            long? newChunkId = await currentChunk.GetChunkIdByPosition(newChunkPosition);
            if (newChunkId == null) {
                _logger.LogWarning("Could not find chunk id for position {Position}!", newChunkPosition);
                return;
            }
            await EnterChunk(GrainFactory.GetGrain<IWorldChunkGrain>(newChunkId.Value));
        }
        
        await _realtimeUpdates.PlayerMovementUpdate(
            await currentChunk.GetRealtimeUpdatesGroupName(),
            this.GetPrimaryKeyString(),
            newPosition.X,
            newPosition.Y
        );
    }

    public async Task DebugMoveToChunk(IWorldChunkGrain chunkGrain) {
        // Make sure we are not following a path anymore
        _path.Clear();

        // Enter the chunk
        // Do this before moving the player, so that we can get the chunk position.
        // Otherwise, the chunk might be outside the "visible radius"
        await EnterChunk(chunkGrain);
        
        // Move player to the center of the chunk
        WorldChunkGrainPosition? chunkGrainPosition = await chunkGrain.GetPosition();
        if (chunkGrainPosition == null) {
            _logger.LogWarning("Could not find position for chunk {ChunkId}!", chunkGrain.GetKey());
            return;
        }
        _playerState.State.Position = new SerializableVector2(
            (chunkGrainPosition.X * WorldChunkGrain.SizeX) + (WorldChunkGrain.SizeX / 2),
            (chunkGrainPosition.Y * WorldChunkGrain.SizeY) + (WorldChunkGrain.SizeY / 2)
        ); 
        await _playerState.WriteStateAsync();
    }
}