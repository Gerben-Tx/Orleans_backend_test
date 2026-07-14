namespace Backend.Orleans.SharedContracts;

[GenerateSerializer]
public record WorldChunkGrainPosition([property: Id(0)] int X, [property: Id(1)] int Y);
