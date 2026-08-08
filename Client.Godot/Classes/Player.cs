using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace Client.Godot.Classes;

public class Player {
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Queue<Vector2>? Path { get; set; }

    public Vector2? GetNextPathPoint() {
        return Path?.Dequeue();
    }

    public void AddPathFromArray(
        Array<Array<int>> path
    ) {
        Path = new Queue<Vector2>(path.ToList().ConvertAll(x => new Vector2(x[0], x[1])));
    }

    public Node3D CreatePlayerNode(
        Vector2 playerPosition,
        Node playersNode
    ) {
        PackedScene playerScene = GD.Load<PackedScene>("res://Player.tscn");
        // Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = playerScene.Instantiate<Node3D>();
        playerNode.Name = Id;
        playerNode.Position = new Vector3(
            playerPosition.X,
            0,
            playerPosition.Y
        );
        playerNode.GetNode<Label3D>("%PlayerNameLabel").Text = Name;
        playersNode.AddChild(playerNode);
        playerNode.Owner = playersNode;

        return playerNode;
    }
}