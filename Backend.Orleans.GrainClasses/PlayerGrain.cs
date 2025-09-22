using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

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

    public async Task EnterChunk(IWorldChunkGrain chunk) {
        // List<string> playersInChunk = await chunk.GetAllPlayers();
        // string playerName = await GetPlayerName();
        // if (playersInChunk.Find(x => x == playerName) != null) {
        //     // Player is already in the new chunk, do nothing
        //     return;
        // }

        IWorldChunkGrain currentChunk = await GetCurrentChunk();

        // Exit from the current chunk
        await currentChunk.RemovePlayer(this.GetPrimaryKey(), await GetPlayerName());

        // Enter the new chunk
        await chunk.AddPlayer(this.GetPrimaryKey(), await GetPlayerName());

        // Update new chunk id in state
        _playerState.State.ChunkGrainId = chunk.GetPrimaryKeyLong();
        await _playerState.WriteStateAsync();

        // Join realtime updates group for this chunk
        string chunkGroupName = await chunk.GetRealtimeUpdatesGroupName();
        await JoinRealtimeUpdatesGroup(chunkGroupName);
    }

    public async Task LeaveChunk(IWorldChunkGrain chunk) {
        // Leave the chunk
        await chunk.RemovePlayer(this.GetPrimaryKey(), await GetPlayerName());

        // Leave the realtime updates group for this chunk
        string chunkGroupName = await chunk.GetRealtimeUpdatesGroupName();
        await LeaveRealtimeUpdatesGroup(chunkGroupName);
    }

    public Task<IWorldChunkGrain> GetCurrentChunk() {
        IWorldChunkGrain worldChunkGrain = GrainFactory.GetGrain<IWorldChunkGrain>(_playerState.State.ChunkGrainId);
        return Task.FromResult(worldChunkGrain);
    }

    public Task<string> GetPlayerName() => Task.FromResult(this.GetPrimaryKeyString());

    public async Task Initialize(string connectionId) {
        _realtimeUpdatesConnectionId = connectionId;

        // Move player to his last known chunk
        IWorldChunkGrain worldChunkGrain = GrainFactory.GetGrain<IWorldChunkGrain>(_playerState.State.ChunkGrainId);
        await EnterChunk(worldChunkGrain);
        await StartMovementTimer();
    }

    public Task<SerializableVector2> GetPosition() {
        return Task.FromResult(_playerState.State.Position);
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
        await _realtimeUpdatesSingleton.OrleansProxy.PlayerMovementUpdate(
            (await GetCurrentChunk()).GetPrimaryKeyString(),
            this.GetPrimaryKeyString(),
            rand.Next(0, WorldChunkGrain.SizeX),
            rand.Next(0, WorldChunkGrain.SizeY)
        );
    }
}