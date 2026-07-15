namespace Backend.SignalR.SharedContracts;

public sealed record WorldChunkNeighborsMessage(
    WorldChunk[] Chunks
);