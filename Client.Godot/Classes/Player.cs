using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace Client.Godot.Classes;

public class Player {
    public string Id { get; private set; }
    public string Name { get; private set; }
    public PlayerNode PlayerNode { get; private set; }
    public Vector2I Position =>
        new(
            (int)PlayerNode.Position.X,
            (int)PlayerNode.Position.Z
        ); // Small wrapper around the PlayerNode.Position to be able to use vector2 in 3d space
    protected Queue<Vector2I>? Path { get; set; }

    public Player(
        string id,
        string name,
        PlayerNode playerNode
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

    public static PlayerNode CreatePlayerNode(
        Vector2 playerPosition,
        Node playersNode,
        string id,
        string name,
        uint serverTicksPerSecond
    ) {
        PackedScene playerScene = GD.Load<PackedScene>("res://Player.tscn");
        // Node playersNode = GetNode<Node>("%Players");
        PlayerNode playerNode = playerScene.Instantiate<PlayerNode>();
        playerNode.Name = id;
        playerNode.Position = new Vector3(playerPosition.X, 0, playerPosition.Y);
        playerNode.GetNode<Label3D>("%PlayerNameLabel").Text = name;
        playersNode.AddChild(playerNode);
        playerNode.Owner = playersNode;
        playerNode.SetTicksPerSecond(serverTicksPerSecond);

        return playerNode;
    }

    public void UpdatePosition(
        Vector2I position
    ) {
        PlayerNode.UpdatePosition(position);
    }
}