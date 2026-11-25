using System.Numerics;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Path = Roy_T.AStar.Paths.Path;

namespace Shared.Pathfinding;

public class PathfindingService {
    private readonly int _cellSize;
    private readonly Grid _grid;

    public PathfindingService(
        int columns,
        int rows,
        int cellSize
    ) {
        _cellSize = cellSize;

        GridSize gridSize = new(
            columns: columns,
            rows: rows
        );
        Size cellSizeValue = new(
            Distance.FromMeters(cellSize),
            Distance.FromMeters(cellSize)
        );
        Velocity traversalVelocity = Velocity.FromMetersPerSecond(1F); // This is not really used for our use case

        // _grid = Grid.CreateGridWithDiagonalConnections(gridSize, cellSizeValue, traversalVelocity);
        // _grid = Grid.CreateGridWithLateralConnections(gridSize, cellSizeValue, traversalVelocity);
        _grid = Grid.CreateGridWithLateralAndDiagonalConnections(gridSize, cellSizeValue, traversalVelocity);
    }

    public Task<Path?> FindPath(
        Vector2 start,
        Vector2 end,
        CancellationToken cancellationToken = default
    ) {
        PathFinder pathFinder = new();
        Path? path = pathFinder.FindPath(
            ToGridPosition(start),
            ToGridPosition(end),
            _grid
        );

        return Task.FromResult<Path?>(path);
    }

    private GridPosition ToGridPosition(
        Vector2 worldPosition
    ) {
        return new GridPosition(
            x: (int)(worldPosition.X / _cellSize),
            y: (int)(worldPosition.Y / _cellSize)
        );
    }

    public Grid GetGrid() {
        return _grid;
    }
}