namespace Backend.SignalR.SharedContracts;

public sealed record WorldChunkNeighborsMessage(
    WorldChunk? North = null,
    WorldChunk? NorthEast = null,
    WorldChunk? East = null,
    WorldChunk? SouthEast = null,
    WorldChunk? South = null,
    WorldChunk? SouthWest = null,
    WorldChunk? West = null,
    WorldChunk? NorthWest = null
) {
    public WorldChunk?[] ToArray() {
        return [North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest];
    }
};