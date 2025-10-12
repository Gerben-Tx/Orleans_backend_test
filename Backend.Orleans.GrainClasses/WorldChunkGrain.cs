using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class WorldChunkGrain : BaseGrain, IWorldChunkGrain {
    private readonly List<string> _players = [];
    public const int SizeX = 30;
    public const int SizeY = 30;
    private readonly string _groupName;
    private readonly ILogger<WorldChunkGrain> _logger;
    private readonly IClusterClient _orleansClient;
    private readonly IRealtimeUpdatesOrleans _realtimeUpdates;

    public WorldChunkGrain(
        ILogger<WorldChunkGrain> logger,
        IClusterClient orleansClient,
        IRealtimeUpdatesOrleans realtimeUpdates
    ) : base(logger) {
        _groupName = this.GetPrimaryKeyLong().ToString();
        _logger = logger;
        _orleansClient = orleansClient;
        _realtimeUpdates = realtimeUpdates;
    }

    public async Task AddPlayer(string playerGrainKey, string playerName, SerializableVector2 playerPosition) {
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

    public async Task RemovePlayer(string playerGrainKey, string playerName) {
        if (!_players.Contains(playerGrainKey)) {
            // Player doesn't exist in this chunk
            return;
        }

        _players.Remove(playerGrainKey);

        await _realtimeUpdates.PlayerRemovedFromChunk(_groupName, playerGrainKey);
    }

    public Task<string> GetRealtimeUpdatesGroupName() => Task.FromResult(_groupName);
    public Task<long> GetKey() => Task.FromResult(this.GetPrimaryKeyLong());
    public Task<bool> IsPlayerInChunk(string playerGrainKey) {
        return Task.FromResult(_players.Contains(playerGrainKey));
    }

    public Task<List<IPlayerGrain>> GetAllPlayers() {
        List<IPlayerGrain> players = [];
        foreach (string playerKey in _players) {
            players.Add(_orleansClient.GetGrain<IPlayerGrain>(Guid.Parse(playerKey)));
        }

        return Task.FromResult(players);
    }
}