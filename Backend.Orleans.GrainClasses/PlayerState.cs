using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.GrainClasses;

[GenerateSerializer]
public record PlayerState {
    [Id(0)] public required string Name { get; set; }
    [Id(1)] public IWorldChunkGrain? ChunkGrain { get; set; }
    [Id(2)] public SerializableVector2 Position { get; set; } = new(0, 0);
}