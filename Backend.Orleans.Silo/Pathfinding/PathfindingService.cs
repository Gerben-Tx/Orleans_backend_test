using System.Numerics;
using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.Silo.Pathfinding;

public class PathfindingService : IPathfindingService {
    private readonly Grid _grid;

    public PathfindingService() {
        GridSize gridSize = new(
            columns: IWorldChunkGrain.WorldSizeX * IWorldChunkGrain.SizeX, 
            rows: IWorldChunkGrain.WorldSizeY * IWorldChunkGrain.SizeY
        );
        Size cellSize = new(Distance.FromMeters(1), Distance.FromMeters(1));
        Velocity traversalVelocity = Velocity.FromMetersPerSecond(1F); // This is not really used for our use case

        _grid = Grid.CreateGridWithDiagonalConnections(gridSize, cellSize, traversalVelocity);
    }
    
    public Task<Path?> FindPath(
        Vector2 start,
        Vector2 end,
        CancellationToken cancellationToken = default
    ) {
        PathFinder pathFinder = new();
        Path? path = pathFinder.FindPath(
            new GridPosition((int)start.X, (int)start.Y),
            new GridPosition((int)end.X, (int)end.Y),
            _grid
        );

        return Task.FromResult<Path?>(path);
    }

    public Grid GetGrid() {
        return _grid;
    }
}