using System.Numerics;
using Backend.Orleans.SharedContracts;
using Roy_T.AStar.Grids;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.Silo.Pathfinding;

public class PathfindingService : IPathfindingService {
    private readonly Shared.Pathfinding.PathfindingService _pathfindingService;

    public PathfindingService() {
        _pathfindingService = new Shared.Pathfinding.PathfindingService(
            columns: IWorldChunkGrain.WorldSizeX * IWorldChunkGrain.SizeX,
            rows: IWorldChunkGrain.WorldSizeY * IWorldChunkGrain.SizeY
        );
    }

    public Task<Path?> FindPath(
        Vector2 start,
        Vector2 end,
        CancellationToken cancellationToken = default
    ) {
        return _pathfindingService.FindPath(start, end, cancellationToken);
    }

    public Grid GetGrid() {
        return _pathfindingService.GetGrid();
    }
}