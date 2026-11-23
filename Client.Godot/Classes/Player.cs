using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace Client.Godot.Classes;

public class Player {
    public string Id { get; private set; }
    public string Name { get; private set; }
    public Node3D PlayerNode { get; private set; }
    public Vector2I Position {
        get => new((int)PlayerNode.Position.X, (int)PlayerNode.Position.Z);
        set => PlayerNode.Position = new Vector3(value.X, PlayerNode.Position.Y, value.Y);
    } // Small wrapper around the PlayerNode.Position to be able to use vector2 in 3d space
    protected Queue<Vector2I>? Path { get; set; }

    public Player(
        string id,
        string name,
        Node3D playerNode
    ) {
        Id = id;
        Name = name;
        PlayerNode = playerNode;
    }

    public Vector2I? GetNextPathPoint() {
        if (Path?.Count > 0) {
            return Path.Dequeue();
        }

        return null;
    }

    public void AddPathFromArray(
        Array<Vector2I> path
    ) {
        Path = new(path);
    }

    public void AddPathFromArray(
        Array<Array<int>> path
    ) {
        Path = new(path.ToList().ConvertAll(x => new Vector2I(x[0], x[1])));
    }

    public static Node3D CreatePlayerNode(
        Vector2 playerPosition,
        Node playersNode,
        string id,
        string name
    ) {
        PackedScene playerScene = GD.Load<PackedScene>("res://Player.tscn");
        // Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = playerScene.Instantiate<Node3D>();
        playerNode.Name = id;
        playerNode.Position = new Vector3(playerPosition.X, 0, playerPosition.Y);
        playerNode.GetNode<Label3D>("%PlayerNameLabel").Text = name;
        playersNode.AddChild(playerNode);
        playerNode.Owner = playersNode;

        return playerNode;
    }
}