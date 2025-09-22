using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Backend.Orleans.GrainClasses;

public class WorldChunkGrain : BaseGrain, IWorldChunkGrain {
    private readonly List<Guid> _players = [];
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

    public async Task AddPlayer(Guid playerGrainKey, string playerName) {
        if (_players.Contains(playerGrainKey)) {
            // Player already exists in this chunk
            return;
        }

        _players.Add(playerGrainKey);
        
        await _realtimeUpdatesSingleton.OrleansProxy.PlayerAddedToChunk(this.GetPrimaryKeyString(), playerName);
    }

    public async Task RemovePlayer(Guid playerGrainKey, string playerName) {
        if (!_players.Contains(playerGrainKey)) {
            // Player doesn't exist in this chunk
            return;
        }

        _players.Remove(playerGrainKey);

        await _realtimeUpdatesSingleton.OrleansProxy.PlayerRemovedFromChunk(this.GetPrimaryKeyString(), playerName);
    }

    public Task<string> GetRealtimeUpdatesGroupName() => Task.FromResult(_groupName);

    public Task<List<IPlayerGrain>> GetAllPlayers() {
        List<IPlayerGrain> players = [];
        foreach (Guid playerKey in _players) {
            players.Add(_orleansClient.GetGrain<IPlayerGrain>(playerKey));
        }

        return Task.FromResult(players);
    }
}