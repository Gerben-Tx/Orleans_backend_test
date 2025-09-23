using Backend.Orleans.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class PlayerRegistry : IPlayerRegistry {
    private readonly IPersistentState<PlayerRegistryState> _playerRegistryState;
    private readonly ILogger<PlayerRegistry> _logger;
    private readonly IClusterClient _orleansClient;

    public PlayerRegistry(
        [PersistentState("playerRegistry", "tableStore")]
        IPersistentState<PlayerRegistryState> playerRegistryState,
        ILogger<PlayerRegistry> logger,
        IClusterClient orleansClient
    ) {
        _playerRegistryState = playerRegistryState;
        _logger = logger;
        _orleansClient = orleansClient;
    }

    public Task AddPlayer(string name, Guid guid) {
        _playerRegistryState.State.Players.Add(name, _orleansClient.GetGrain<IPlayerGrain>(guid));

        _logger.LogDebug("Added player '{PlayerName}' to playerRegistry with guid '{Guid}'.", name, guid);

        return _playerRegistryState.WriteStateAsync();
    }

    public Task RemovePlayer(string name) {
        _playerRegistryState.State.Players.Remove(name);

        _logger.LogDebug("Removed player '{PlayerName}' from the playerRegistry.", name);

        return _playerRegistryState.WriteStateAsync();
    }

    public Task<IPlayerGrain?> FindPlayerByName(string name) {
        bool playerFound =
            _playerRegistryState.State.Players.TryGetValue(name, out IPlayerGrain? playerGrain);
        if (!playerFound) {
            return Task.FromResult<IPlayerGrain?>(null);
        }

        return Task.FromResult(playerGrain);
    }
}