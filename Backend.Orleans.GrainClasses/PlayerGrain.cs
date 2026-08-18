using System.Numerics;
using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;
using Roy_T.AStar.Graphs;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.GrainClasses;

public class PlayerGrain : BaseGrain, IPlayerGrain {
    private readonly List<PlayerInput> _inputs = new();
    private readonly ILogger<PlayerGrain> _logger;
    private readonly Queue<SerializableVector2> _path = new();
    private readonly IPathfindingService _pathFindingService;
    private readonly IPersistentState<PlayerState> _playerState;
    private readonly IRealtimeUpdatesOrleans _realtimeUpdates;
    private readonly ITickManager _tickManager;
    private int _chunkVisibileRadius = 1;
    private string? _realtimeUpdatesConnectionId;
    private Vector2? _targetDestination = null;

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
            await targetChunk.AddPlayer(
                this.GetPrimaryKeyString(),
                await GetName(),
                _playerState.State.Position,
                _path);

            await Task.WhenAll(parallelizeTasks);
        }

        // Update new chunk id in state
        if (_playerState.State.ChunkGrain != targetChunk) {
            _playerState.State.ChunkGrain = targetChunk;
            await _playerState.WriteStateAsync();
        }
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
        // if (_inputs.Count == 0) {
        //     return;
        // }

        // Dequeue inputs and apply them
        _inputs.RemoveAll(input => input.Tick < _tickManager.GetTicks()); // Remove old inputs
        List<PlayerInput> inputsForThisTick = _inputs.Where(input => input.Tick == _tickManager.GetTicks()).ToList();
        foreach (PlayerInput playerInput in inputsForThisTick) {
            _inputs.Remove(playerInput);
            playerInput.Apply(this);
        }

        // Simulate movement
        await MovementUpdate();
    }

    public Task ReceiveMovementIntent(
        int destinationX,
        int destinationY,
        ulong tick
    ) {
        _inputs.Add(new MovementInput(destinationX, destinationY, tick));

        return Task.CompletedTask;
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

    public async Task DebugMoveToChunk(
        IWorldChunkGrain chunkGrain
    ) {
        // Make sure we are not following a path anymore
        _path.Clear();


        #region Move player to the center of the chunk

        // We update the player position first so that when EnterChunk
        // sends its broadcast, the new player position is send with it
        // and the player is rendered in the chunk, rather than on it's old pos

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

        #endregion

        await EnterChunk(chunkGrain);
    }

    public async Task CreateNewPathAndNotify(
        int destinationX,
        int destinationY
    ) {
        // Clear current path
        _path.Clear();

        IWorldChunkGrain currentChunk = await GetCurrentChunk();

        // Random rand = new();
        Path? path = await _pathFindingService.FindPath(
            _playerState.State.Position.ToVector2(),
            new Vector2(
                // rand.Next(0, _pathFindingService.GetGrid().Columns),
                // rand.Next(0, _pathFindingService.GetGrid().Rows)
                destinationX,
                destinationY
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

        _tickManager.UnregisterTickCallback(TickCallback);
    }

    private void TickCallback() {
        // Make this grain send a message to itself to call OnTickAsync
        // If we called OnTickAsync directly, it wouldn't work
        // because the TickManager cannot execute grain code
        // because it runs outside the grain context (on a thread-pool thread)
        this.AsReference<IPlayerGrain>().OnTickAsync();
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
        // If we somehow have no path, just return
        if (_path.Count == 0) {
            // _logger.LogWarning("Path is empty!");
            return;
        }

        IWorldChunkGrain currentChunk = await GetCurrentChunk();

        // _logger.LogDebug("Sending path movement update...");
        SerializableVector2 newPosition = _path.Dequeue();

        _playerState.State.Position = newPosition; // Save position in state
        await _playerState.WriteStateAsync();

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
}