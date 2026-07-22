namespace Backend.Orleans.SharedContracts;

[GenerateSerializer]
public record VisibleWorldChunk([property: Id(0)] long Id, [property: Id(1)] WorldChunkGrainPosition Position);
