using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class PlayerGrain : BaseGrain, IPlayerGrain {
    private readonly IPersistentState<PlayerState> _playerState;
    private readonly ILogger<PlayerGrain> _logger;
    private string? _realtimeUpdatesConnectionId;
    private readonly RealtimeUpdatesSingleton _realtimeUpdatesSingleton;

    public PlayerGrain(
        [PersistentState("player", "tableStore")]
        IPersistentState<PlayerState> playerState,
        ILogger<PlayerGrain> logger
    ) : base(logger) {
        _playerState = playerState;
        _logger = logger;
        _realtimeUpdatesSingleton = RealtimeUpdatesSingleton.Instance;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken) {
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken) {
        await LeaveChunk(await GetCurrentChunk());

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task EnterChunk(IWorldChunkGrain targetChunk) {
        // List<string> playersInChunk = await chunk.GetAllPlayers();
        // string playerName = await GetPlayerName();
        // if (playersInChunk.Find(x => x == playerName) != null) {
        //     // Player is already in the new chunk, do nothing
        //     return;
        // }

        IWorldChunkGrain currentChunk = await GetCurrentChunk();
        if (currentChunk != targetChunk) {
            // Exit from the current chunk
            await LeaveChunk(currentChunk);

            // Enter the new chunk
            await targetChunk.AddPlayer(this.GetPrimaryKeyString(), await GetName(), _playerState.State.Position);
            
            // Join realtime updates group for this chunk
            string chunkGroupName = await targetChunk.GetRealtimeUpdatesGroupName();
            await JoinRealtimeUpdatesGroup(chunkGroupName);
        }

        // Update new chunk id in state
        if (_playerState.State.ChunkGrain != targetChunk) {
            _playerState.State.ChunkGrain = targetChunk;
            await _playerState.WriteStateAsync();
        }
    }

    public async Task LeaveChunk(IWorldChunkGrain chunk) {
        // Leave the chunk
        await chunk.RemovePlayer(this.GetPrimaryKeyString(), await GetName());

        // Leave the realtime updates group for this chunk
        string chunkGroupName = await chunk.GetRealtimeUpdatesGroupName();
        await LeaveRealtimeUpdatesGroup(chunkGroupName);
    }

    public Task<IWorldChunkGrain> GetCurrentChunk() {
        return Task.FromResult(_playerState.State.ChunkGrain ?? GrainFactory.GetGrain<IWorldChunkGrain>(0L));
    }

    public async Task Initialize(string connectionId, string playerName) {
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

    public async Task JoinRealtimeUpdatesGroup(string groupName) {
        if (_realtimeUpdatesConnectionId == null) {
            return;
        }

        await _realtimeUpdatesSingleton.OrleansProxy.AddToGroupAsync(groupName, _realtimeUpdatesConnectionId);
    }

    public async Task LeaveRealtimeUpdatesGroup(string groupName) {
        if (_realtimeUpdatesConnectionId == null) {
            return;
        }

        await _realtimeUpdatesSingleton.OrleansProxy.RemoveFromGroupAsync(groupName, _realtimeUpdatesConnectionId);
    }

    public Task StartMovementTimer() {
        IDisposable timer = this.RegisterGrainTimer(SendRandomMovementUpdate,
            new GrainTimerCreationOptions(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5))
                { Interleave = true, KeepAlive = true });

        return Task.CompletedTask;
    }

    private async Task SendRandomMovementUpdate() {
        // Send random movement updates to clients as a test
        _logger.LogDebug("Sending random movement update...");
        
        Random rand = new();
        SerializableVector2 newPosition = new(
            rand.Next(-WorldChunkGrain.SizeX / 2, WorldChunkGrain.SizeX / 2),
            rand.Next(-WorldChunkGrain.SizeY / 2, WorldChunkGrain.SizeY / 2)
        );

        _playerState.State.Position = newPosition;
        await _playerState.WriteStateAsync();
        IWorldChunkGrain currentChunk = await GetCurrentChunk();

        await _realtimeUpdatesSingleton.OrleansProxy.PlayerMovementUpdate(
            await currentChunk.GetRealtimeUpdatesGroupName(),
            this.GetPrimaryKeyString(),
            newPosition.X,
            newPosition.Y
        );
    }
}