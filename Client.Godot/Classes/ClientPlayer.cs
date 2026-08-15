using Godot;

namespace Client.Godot.Classes;

public class ClientPlayer : Player {
    private readonly ClientSimulation _clientSimulation;

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
            ServerCommunicator.Instance.PlayerName,
            position.X,
            position.Y,
            _clientSimulation.Ticks
        );
    }
}