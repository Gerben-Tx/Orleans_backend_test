using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;
using Roy_T.AStar.Paths;
using Shared.Pathfinding;
using Vector2 = System.Numerics.Vector2;

namespace Client.Godot.Classes;

public class ClientPlayer : Player {
    private readonly ClientSimulation _clientSimulation;
    private readonly PathfindingService _pathfindingService;
    private readonly RandomNumberGenerator _randomNumberGenerator = new();

    public ClientPlayer(
        string id,
        string name,
        Node3D playerNode,
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
        ServerCommunicator.Instance.HubProxy.SendMovementIntent(
            Name,
            newPosition.X,
            newPosition.Y,
            _clientSimulation.Ticks
        );

        // Use pathfinding service to get the path locally
        Task<Path?> path = _pathfindingService.FindPath(
            new Vector2(Position.X, Position.Y),
            new Vector2(newPosition.X, newPosition.Y)
        );

        // Give new path to player
        path.ContinueWith(task => {
            if (task.Result != null) {
                Path = new Queue<global::Godot.Vector2>(
                    task.Result.Edges.ToList()
                        .ConvertAll(x => new global::Godot.Vector2(x.End.Position.X, x.End.Position.Y)));
            }
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
}