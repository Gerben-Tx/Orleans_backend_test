namespace Backend.SignalR.SharedContracts;

public sealed record VisibleWorldChunksMessage(
    WorldChunkContract[] Chunks
);