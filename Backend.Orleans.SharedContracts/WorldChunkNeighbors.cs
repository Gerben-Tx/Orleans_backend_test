namespace Backend.Orleans.SharedContracts;

[GenerateSerializer]
public record WorldChunkNeighbors(
    [property: Id(0)] WorldChunkNeighbor? North = null,
    [property: Id(1)] WorldChunkNeighbor? NorthEast = null,
    [property: Id(2)] WorldChunkNeighbor? East = null,
    [property: Id(3)] WorldChunkNeighbor? SouthEast = null,
    [property: Id(4)] WorldChunkNeighbor? South = null,
    [property: Id(5)] WorldChunkNeighbor? SouthWest = null,
    [property: Id(6)] WorldChunkNeighbor? West = null,
    [property: Id(7)] WorldChunkNeighbor? NorthWest = null
) {
    public WorldChunkNeighbor?[] ToArray() {
        return [North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest];
    }
}
