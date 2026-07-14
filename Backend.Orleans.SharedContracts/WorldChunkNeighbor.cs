namespace Backend.Orleans.SharedContracts;

[GenerateSerializer]
public record WorldChunkNeighbor([property: Id(0)] long Id, [property: Id(1)] WorldChunkGrainPosition Position);
