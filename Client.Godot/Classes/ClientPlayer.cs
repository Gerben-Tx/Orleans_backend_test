using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;
using Godot.Collections;
using Roy_T.AStar.Paths;
using Shared.Pathfinding;

namespace Client.Godot.Classes;

public class ClientPlayer : Player {
    private readonly ClientSimulation _clientSimulation;
    private readonly PathfindingService _pathfindingService;
    private readonly RandomNumberGenerator _randomNumberGenerator = new();
    private ulong _lastSendMovementIntentId = 0;
    /// <summary>
    /// Contains the whole path we generated ourselves. This includes vectors we may have already visited.
    /// Used to compare against the path we get from the server to see if we need to update our path or not.
    /// </summary>
    private Array<Vector2I>? PredictedPath { get; set; }
    private int _remainingPointsOnPath => Path?.Count ?? 0;

    public ClientPlayer(
        string id,
        string name,
        PlayerNode playerNode,
        ClientSimulation clientSimulation,
        PathfindingService pathfindingService
    ) : base(id, name, playerNode) {
        _clientSimulation = clientSimulation;
        _pathfindingService = pathfindingService;

        PlayerNode.GetNode<Camera3D>("Camera3D").Current = true;
    }

    public ClientPlayer(
        ClientSimulation clientSimulation,
        Player player,
        PathfindingService pathfindingService
    ) : this(player.Id, player.Name, player.PlayerNode, clientSimulation, pathfindingService) { }

    public void MoveTo(
        Vector2I newPosition
    ) {
        GD.Print($"Sending movement intent ({newPosition.X},{newPosition.Y},{_clientSimulation.Ticks})");
        ServerCommunicator.Instance.HubProxy.SendMovementIntent(
            Name,
            newPosition.X,
            newPosition.Y,
            _clientSimulation.Ticks,
            ++_lastSendMovementIntentId
        );

        ApplyClientSidePrediction(newPosition);
    }

    private void ApplyClientSidePrediction(
        Vector2I newPosition
    ) {
        // Use pathfinding service to get the path locally
        Task<Path?> path = _pathfindingService.FindPath(
            new System.Numerics.Vector2(Position.X, Position.Y),
            new System.Numerics.Vector2(newPosition.X, newPosition.Y)
        );

        // Give new path to player
        path.ContinueWith(task => {
            if (task.Result == null) {
                Path = null;
                PredictedPath = null;

                return;
            }

            Path = new Queue<Vector2I>(
                task.Result.Edges.ToList()
                    .ConvertAll(input => new Vector2I((int)input.End.Position.X, (int)input.End.Position.Y)));
            PredictedPath = (Array<Vector2I>)[.. Path];
        });
    }

    public void HandleTick(
        WorldInfoMessage worldInfo
    ) {
#if DEBUG

        #region Move debug clients into random positions

        if (DisplayServer.WindowCanDraw()) {
            // Ignore real clients
            // so we only move debug clients
            return;
        }

        if (Path?.Count > 0) {
            return;
        }

        Vector2I randomPos = new(
            _randomNumberGenerator.RandiRange(0, worldInfo.WorldSizeX * worldInfo.ChunkSizeX),
            _randomNumberGenerator.RandiRange(0, worldInfo.WorldSizeY * worldInfo.ChunkSizeY)
        );
        MoveTo(randomPos);

        #endregion

#endif
    }

    public void ApplyServerReconciliation(
        Array<Vector2I> serverPath,
        ulong? id
    ) {
        if (id != null && id != _lastSendMovementIntentId) {
            // If we have an id and it doesn't match the last send movement intent id, ignore the new path
            GD.Print("Ignoring new path with id: ", id);
            return;
        }

        // Override path if we don't have one yet locally
        if (Path == null || PredictedPath == null) {
            GD.Print("Override path because we don't have one yet locally");
            AddPathFromArray(serverPath);
            return;
        }

        // Check if server path matches predicted path
        Array<Vector2I> serverPathSliced = serverPath.GetSliceRange(
            _remainingPointsOnPath * -1,
            serverPath.Count); // Only verify the remaining points on the path
        Array<Vector2I> predictedPathSliced = PredictedPath.GetSliceRange(
            _remainingPointsOnPath * -1,
            PredictedPath.Count);

#if DEBUG
        GD.Print($"Remaining points on path: {_remainingPointsOnPath}");
        GD.Print($"Current position: [{Position.X}, {Position.Y}]");
        GD.Print(
            $"Current path:          {string.Join(" | ", Path.Select(row => $"[{string.Join(", ", row)}]"))}");
        GD.Print(
            $"Predicted path:        {string.Join(" | ", PredictedPath.Select(row => $"[{string.Join(", ", row)}]"))}");
        GD.Print(
            $"Predicted sliced path: {string.Join(" | ", predictedPathSliced.Select(row => $"[{string.Join(", ", row)}]"))}");
        GD.Print(
            $"Server path:           {string.Join(" | ", serverPath.Select(row => $"[{string.Join(", ", row)}]"))}");
        GD.Print(
            $"Server sliced path:    {string.Join(" | ", serverPathSliced.Select(row => $"[{string.Join(", ", row)}]"))}");
#endif

        foreach (Vector2I serverVector in serverPathSliced) {
            int serverIndex = serverPathSliced.IndexOf(serverVector);

            // Validate that the client vector and server vector match
            Vector2 clientVector = predictedPathSliced[serverIndex];
            if (clientVector.Equals(serverVector)) {
                // Client vector and server match, so continue to next vector
#if DEBUG
                GD.Print("Predicted vector and server match, so continue to next vector");
#endif
                continue;
            }

            // Predicted path doesn't match server path, override path with server path
            GD.Print("Predicted path doesn't match server path, override path with server path");
#if DEBUG
            DebugDraw2D.Config.TextBlockOffset = new Vector2I(
                (int)PlayerNode.GetViewport().GetVisibleRect().Size.X / 2,
                (int)PlayerNode.GetViewport().GetVisibleRect().Size.Y / 2);
            DebugDraw2D.SetText("Overriding path!", duration: 3);
#endif
            PredictedPath = null;
            // AddPathFromArray(serverPathSliced);
            AddPathFromArray(serverPath);
            // TODO: Using `serverPath` causes way more teleporting compared to using `serverPathSliced`
            //  This is expected because that path is usually longer than the sliced path.
            //  But do we really want the longer path? 
            //  I think so.. otherwise we might be following an "illegal" path which has the same destination
            //  So how would we solve the issue of teleporting back?
            //  Should we wait in place like suggested below?

            break;
        }

        // TODO: if we are more than X points away from the server pos, we should teleport back / wait in place
        //  For this we need to know what the current server position is.
        //  We could determine this from the server path, since it starts from the current position (or close to it).
        //  If our current position is more than X points away from the server path start, we should teleport back / wait in place.

        // TODO: should we update the CachedPath with the server path if all vectors match?

        // TODO: issue:
        //  When you click 3 times on different places with a ~second in between, the paths don't match 
        //  Example;
        //   Remaining points on path: 0
        //   Current position: [245, 45]
        //   Current path:
        //   Predicted path:        [(252, 40)] | [(251, 41)] | [(250, 42)] | [(249, 43)] | [(248, 44)] | [(247, 44)] | [(246, 45)] | [(245, 45)]
        //   Predicted sliced path: [(252, 40)] | [(251, 41)] | [(250, 42)] | [(249, 43)] | [(248, 44)] | [(247, 44)] | [(246, 45)] | [(245, 45)]
        //   Server path:           [(253, 38)] | [(252, 39)] | [(251, 40)] | [(250, 41)] | [(249, 42)] | [(248, 43)] | [(247, 44)] | [(246, 45)] | [(245, 45)]
        //   Server sliced path:    [(253, 38)] | [(252, 39)] | [(251, 40)] | [(250, 41)] | [(249, 42)] | [(248, 43)] | [(247, 44)] | [(246, 45)] | [(245, 45)]
        //  I think this is due to the client already being somewhere else when creating it's 3rd path.
        //  To fix this we can 
    }
}