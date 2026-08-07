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
    private readonly ITickManager _tickManager;

    public PlayerGrain(
        [PersistentState("player", "tableStore")]
        IPersistentState<PlayerState> playerState,
        ILogger<PlayerGrain> logger,
        IRealtimeUpdatesOrleans realtimeUpdates,
        IPathfindingService pathfindingService,
        ITickManager tickManager
    ) : base(logger) {
        _playerState = playerState;
        _logger = logger;
        _realtimeUpdates = realtimeUpdates;
        _pathFindingService = pathfindingService;
        _tickManager = tickManager;
    }

    public override async Task OnActivateAsync(
        CancellationToken cancellationToken
    ) {
        await base.OnActivateAsync(cancellationToken);

        _tickManager.RegisterTickCallback(TickCallback);
    }

    public override async Task OnDeactivateAsync(
        DeactivationReason reason,
        CancellationToken cancellationToken
    ) {
        await LeaveChunk(await GetCurrentChunk());

        await base.OnDeactivateAsync(reason, cancellationToken);

        // TODO: Test this!
        _tickManager.UnregisterTickCallback(TickCallback);
    }

    private void TickCallback() {
        // Make this grain send a message to itself to call OnTickAsync
        // If we called OnTickAsync directly, it wouldn't work
        // because the TickManager cannot execute grain code
        // because it runs outside the grain context (on a thread-pool thread)
        this.AsReference<IPlayerGrain>().OnTickAsync();
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
            await targetChunk.AddPlayer(this.GetPrimaryKeyString(), await GetName(), _playerState.State.Position, _path);

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

        // Join players own realtime updates group (for things like ticks)
        await JoinRealtimeUpdatesGroup(await GetKey());

        // Move player to his last known chunk
        await EnterChunk(await GetCurrentChunk());
    }

    public async Task OnTickAsync() {
        await MovementUpdate();
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

    private async Task MovementUpdate() {
        IWorldChunkGrain currentChunk = await GetCurrentChunk();
        
        if (_path.Count == 0) {
            _logger.LogDebug("No path found, creating new path...");
            Random rand = new();
            Path? path = await _pathFindingService.FindPath(
                _playerState.State.Position.ToVector2(),
                new Vector2(
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

            await _realtimeUpdates.PlayerNewPathCreated(
                await currentChunk.GetRealtimeUpdatesGroupName(),
                this.GetPrimaryKeyString(),
                _path.ToList().ConvertAll<int[]>(x => x.ToArray()).ToArray()
            );
        }

        // If we somehow have no path, just return
        if (_path.Count == 0) {
            _logger.LogWarning("Path is empty!");
            return;
        }

        // _logger.LogDebug("Sending path movement update...");
        SerializableVector2 newPosition = _path.Dequeue();

        _playerState.State.Position = newPosition; // Save position in state
        
        WorldChunkGrainPosition? currentChunkPosition = await currentChunk.GetPosition();
        if (currentChunkPosition == null) {
            _logger.LogWarning("Could not find position for chunk {ChunkId}!", currentChunk.GetKey());
            return;
        }

        var newChunkPosition = new WorldChunkGrainPosition(
            newPosition.X / IWorldChunkGrain.SizeX,
            newPosition.Y / IWorldChunkGrain.SizeY
        );

        // Enter new chunk if we cross borders
        if (newChunkPosition != currentChunkPosition) {
            _logger.LogDebug(
                "Current chunk position: {CurrentChunkPosition} | New chunk position: {NewChunkPosition}",
                currentChunkPosition,
                newChunkPosition);

            long? newChunkId = await currentChunk.GetChunkIdByPosition(newChunkPosition);
            if (newChunkId == null) {
                _logger.LogWarning("Could not find chunk id for position {Position}!", newChunkPosition);
                return;
            }

            await EnterChunk(GrainFactory.GetGrain<IWorldChunkGrain>(newChunkId.Value));
        }
    }

    public async Task DebugMoveToChunk(
        IWorldChunkGrain chunkGrain
    ) {
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
            (chunkGrainPosition.X * IWorldChunkGrain.SizeX) + (IWorldChunkGrain.SizeX / 2),
            (chunkGrainPosition.Y * IWorldChunkGrain.SizeY) + (IWorldChunkGrain.SizeY / 2)
        );
        await _playerState.WriteStateAsync();
    }
}