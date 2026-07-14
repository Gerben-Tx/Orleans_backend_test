namespace Backend.SignalR.SharedContracts;

public sealed record WorldChunk(
    long ChunkId,
    int X,
    int Y
);