using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Backend.Orleans.GrainClasses;

public class PlayerGrain : BaseGrain, IPlayerGrain {
    private readonly IPersistentState<PlayerState> _playerState;
    private readonly ILogger<PlayerGrain> _logger;
    private string? _realtimeUpdatesConnectionId;

    public PlayerGrain(
        [PersistentState("player", "tableStore")]
        IPersistentState<PlayerState> playerState,
        ILogger<PlayerGrain> logger
    ) : base(logger) {
        _playerState = playerState;
        _logger = logger;
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

        IAsyncStream<IStreamMessage> stream =
            this.GetStreamProvider("StreamProvider")
                .GetStream<IStreamMessage>("MAIN_STREAM", "PLAYER");
        await stream.OnNextAsync(new StreamMessage("AddToGroupAsync", _realtimeUpdatesConnectionId, groupName));
    }

    public async Task LeaveRealtimeUpdatesGroup(string groupName) {
        if (_realtimeUpdatesConnectionId == null) {
            return;
        }

        IAsyncStream<IStreamMessage> stream =
            this.GetStreamProvider("StreamProvider")
                .GetStream<IStreamMessage>("MAIN_STREAM", "PLAYER");
        await stream.OnNextAsync(new StreamMessage("RemoveFromGroupAsync", _realtimeUpdatesConnectionId, groupName));
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

        IAsyncStream<IStreamMessage> stream =
            this.GetStreamProvider("StreamProvider")
                .GetStream<IStreamMessage>("MAIN_STREAM", "PLAYER");
        Random rand = new();
        await stream.OnNextAsync(new StreamMessage(
                "PlayerMovementUpdate",
                new object[] {
                    this.GetPrimaryKeyString(),
                    new SerializableVector2(
                        rand.Next(0, WorldChunkGrain.SizeX),
                        rand.Next(0, WorldChunkGrain.SizeY)
                    )
                },
                (await GetCurrentChunk()).GetPrimaryKeyString()
            )
        );
    }
}