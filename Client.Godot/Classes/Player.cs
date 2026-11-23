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
    protected Queue<Vector2>? Path { get; set; }
    /// <summary>
    /// Contains the whole path we generated ourselves. This includes vectors we may have already visited.
    /// Used to compare against the path we get from the server to see if we need to update our path or not.
    /// </summary>
    protected List<Vector2>? CachedPath { get; set; }

    public Player(
        string id,
        string name,
        Node3D playerNode
    ) {
        Id = id;
        Name = name;
        PlayerNode = playerNode;
    }

    public Vector2? GetNextPathPoint() {
        if (Path?.Count > 0) {
            return Path.Dequeue();
        }

        return null;
    }

    public void AddPathFromArray(
        Array<Array<int>> path
    ) {
        Queue<Vector2> newPath = new(path.ToList().ConvertAll(x => new Vector2(x[0], x[1])));

        // Override path if we don't have one yet locally
        if (Path == null || CachedPath == null) {
            GD.Print("Override path because we don't have one yet locally");
            Path = newPath;
            return;
        }

        // Check if server path matches client path
        foreach (Array<int> vectorArray in path) {
            Vector2 serverVector = new(vectorArray[0], vectorArray[1]);
            int serverIndex = path.IndexOf(vectorArray);

            Vector2 clientVector = CachedPath.ToList()[serverIndex];
            if (clientVector.Equals(serverVector)) {
                // Client vector and server match, so continue to next vector
                // GD.Print("Client vector and server match, so continue to next vector");
                continue;
            }

            // Client path doesn't match server path, override client path with server path
            GD.Print("Client path doesn't match server path, override client path with server path");
            GD.Print($"Client path: {string.Join(" | ", CachedPath.Select(row => $"[{string.Join(", ", row)}]"))}");
            GD.Print($"Server path: {string.Join(" | ", newPath.Select(row => $"[{string.Join(", ", row)}]"))}");
            Path = newPath;
            CachedPath = null;
            break;
        }
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