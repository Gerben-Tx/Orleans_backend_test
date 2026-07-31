namespace Backend.SignalR.SharedContracts;

public sealed record WorldChunkContract(
    long ChunkId,
    int X,
    int Y
);