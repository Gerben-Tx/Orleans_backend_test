using System.Numerics;
using Roy_T.AStar.Grids;
using Path = Roy_T.AStar.Paths.Path;

namespace Backend.Orleans.SharedContracts;

public interface IPathfindingService {
    Task<Path?> FindPath(
        Vector2 start,
        Vector2 end,
        CancellationToken cancellationToken = default
    );
    
    Grid GetGrid();
}