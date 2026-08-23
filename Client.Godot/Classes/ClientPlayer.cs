using Backend.SignalR.SharedContracts;
using Godot;

namespace Client.Godot.Classes;

public class ClientPlayer : Player {
    private readonly ClientSimulation _clientSimulation;
    private readonly RandomNumberGenerator _randomNumberGenerator = new();

    public ClientPlayer(
        string id,
        string name,
        Node3D playerNode,
        ClientSimulation clientSimulation
    ) : base(id, name, playerNode) {
        _clientSimulation = clientSimulation;

        PlayerNode.GetNode<Camera3D>("Camera3D").Current = true;
    }

    public ClientPlayer(
        ClientSimulation clientSimulation,
        Player player
    ) : this(player.Id, player.Name, player.PlayerNode, clientSimulation) { }

    public void MoveTo(
        Vector2I position
    ) {
        ServerCommunicator.Instance.HubProxy.SendMovementIntent(
            Name,
            position.X,
            position.Y,
            _clientSimulation.Ticks
        );
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