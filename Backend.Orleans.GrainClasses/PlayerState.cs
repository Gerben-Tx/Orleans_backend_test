using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.GrainClasses;

[GenerateSerializer]
public record PlayerState {
    [Id(0)] public required string Name { get; set; }
    [Id(1)] public long ChunkGrainId { get; set; }
    [Id(2)] public SerializableVector2 Position { get; set; } = new(0, 0);
}