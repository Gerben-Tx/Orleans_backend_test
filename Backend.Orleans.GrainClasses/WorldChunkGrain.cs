using Backend.Orleans.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class WorldChunkGrain : BaseGrain, IWorldChunkGrain {
    private readonly List<string> _players = [];
    public const int SizeX = 30;
    public const int SizeY = 30;
    private readonly string _groupName;
    private readonly IClusterClient _orleansClient;
    private readonly RealtimeUpdatesSingleton _realtimeUpdatesSingleton;

    public WorldChunkGrain(
        ILogger<WorldChunkGrain> logger,
        IClusterClient orleansClient
    ) : base(logger) {
        _groupName = this.GetPrimaryKeyString();
        _orleansClient = orleansClient;
        _realtimeUpdatesSingleton = RealtimeUpdatesSingleton.Instance;
    }

    public async Task AddPlayer(string playerGrainKey, string playerName) {
        if (_players.Contains(playerGrainKey)) {
            // Player already exists in this chunk
            return;
        }

        _players.Add(playerGrainKey);

        await _realtimeUpdatesSingleton.OrleansProxy.PlayerAddedToChunk(
            this.GetPrimaryKeyString(),
            playerGrainKey,
            playerName
        );
    }

    public async Task RemovePlayer(string playerGrainKey, string playerName) {
        if (!_players.Contains(playerGrainKey)) {
            // Player doesn't exist in this chunk
            return;
        }

        _players.Remove(playerGrainKey);

        await _realtimeUpdatesSingleton.OrleansProxy.PlayerRemovedFromChunk(this.GetPrimaryKeyString(), playerGrainKey);
    }

    public Task<string> GetRealtimeUpdatesGroupName() => Task.FromResult(_groupName);

    public Task<List<IPlayerGrain>> GetAllPlayers() {
        List<IPlayerGrain> players = [];
        foreach (string playerKey in _players) {
            players.Add(_orleansClient.GetGrain<IPlayerGrain>(Guid.Parse(playerKey)));
        }

        return Task.FromResult(players);
    }
}