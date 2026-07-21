namespace Backend.SignalR.SharedContracts;

public sealed record WorldChunkNeighborsMessage(
    WorldChunkContract[] Chunks
);