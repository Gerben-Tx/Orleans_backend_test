using Backend.Orleans.SharedContracts;

namespace Backend.Orleans.GrainClasses;

[GenerateSerializer]
public record PlayerRegistryState {
    [Id(0)]
    public Dictionary<string, IPlayerGrain> Players { get; init; } = new();
}