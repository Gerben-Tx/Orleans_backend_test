namespace Backend.Orleans.GrainClasses;

[GenerateSerializer]
public record PlayerRegistryState {
    [Id(0)]
    public Dictionary<string, Guid> Players { get; init; } = new();
}