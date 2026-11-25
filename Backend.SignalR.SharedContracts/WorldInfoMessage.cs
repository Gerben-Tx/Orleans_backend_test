namespace Backend.SignalR.SharedContracts;

public sealed record WorldInfoMessage(
    int WorldSizeX,
    int WorldSizeY,
    int ChunkSizeX,
    int ChunkSizeY,
    int CellSize,
    ulong CurrentTick,
    uint TicksPerSecond
);