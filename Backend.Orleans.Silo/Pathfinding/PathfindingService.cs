using System.Numerics;
using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.Silo.Pathfinding;

public class PathfindingService : IPathfindingService {
    public Task<Path?> FindPath(
        Vector2 start,
        Vector2 end,
        CancellationToken cancellationToken = default
    ) {
        GridSize gridSize = new(columns: WorldChunkGrain.SizeY, rows: WorldChunkGrain.SizeX);
        Size cellSize = new(Distance.FromMeters(1), Distance.FromMeters(1));
        Velocity traversalVelocity = Velocity.FromMetersPerSecond(1.65F); // TODO: No clue what to use here... 

        Grid? grid = Grid.CreateGridWithDiagonalConnections(gridSize, cellSize, traversalVelocity);

        PathFinder pathFinder = new();
        Path? path = pathFinder.FindPath(
            new GridPosition((int)start.X, (int)start.Y),
            new GridPosition((int)end.X, (int)end.Y),
            grid
        );

        return Task.FromResult<Path?>(path);
    }
}