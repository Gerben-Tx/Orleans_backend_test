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
            columns: WorldChunkGrain.WorldSizeX * WorldChunkGrain.SizeY, 
            rows: WorldChunkGrain.WorldSizeY * WorldChunkGrain.SizeX
        );
        Size cellSize = new(Distance.FromMeters(1), Distance.FromMeters(1));
        Velocity traversalVelocity = Velocity.FromMetersPerSecond(1.65F); // TODO: No clue what to use here... 

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