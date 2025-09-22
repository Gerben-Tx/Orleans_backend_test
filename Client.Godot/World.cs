using System.Collections.Generic;
using System.Text.Json;
using Backend.SignalR.SharedContracts;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Godot;

public partial class World : Node3D {
    private RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

    public async override void _Ready() {
        base._Ready();

        GD.Print("Requesting current chunk id...");
        long currentChunkId =
            await ServerCommunicator.Connection.InvokeCoreAsync<long>("GetCurrentChunkId",
                [ServerCommunicator.PlayerName]);
        GD.Print($"Current Chunk ID: {currentChunkId}");

        Label chunkLabel = GetNode<Label>("%ChunkLabel");
        chunkLabel.Text = chunkLabel.Text.Replace("{id}", currentChunkId.ToString());

        ServerCommunicator.Connection.On<string>("PlayerAddedToChunk",
            receivedPlayerName => { CallDeferred(nameof(HandlePlayerAddedToChunk), receivedPlayerName); });

        ServerCommunicator.Connection.On<string>("PlayerRemovedFromChunk",
            receivedPlayerName => { CallDeferred(nameof(HandlePlayerRemovedFromChunk), receivedPlayerName); });

        // TODO: use typed messages here (everywhere with signalr..)
        ServerCommunicator.Connection.On<object[]>("PlayerMovementUpdate",
            receivedData => {
                JsonElement root = JsonDocument.Parse(receivedData[1].ToString()).RootElement;
                CallDeferred(nameof(HandlePlayerMovementUpdate), receivedData[0].ToString(), new Vector2(
                    root.GetProperty("x").GetSingle(),
                    root.GetProperty("y").GetSingle()
                ));
            });

        // Load all players in chunk
        List<PlayerListMessage> playersInChunk =
            await ServerCommunicator.Connection.InvokeCoreAsync<List<PlayerListMessage>>("GetPlayersInCurrentChunk",
                [ServerCommunicator.PlayerName]);
        GD.Print($"Players in chunk: {playersInChunk.Count}");
        foreach (PlayerListMessage playerListMessage in playersInChunk) {
            CreatePlayer(playerListMessage.Name, new Vector2(playerListMessage.PositionX, playerListMessage.PositionY));
        }
    }

    public async void _on_go_to_chunk_button_button_up() {
        GD.Print("_on_go_to_chunk_button_button_up");

        LineEdit newChunkIdInput = GetNode<LineEdit>("%NewChunkIdInput");
        int newChunkId = int.Parse(newChunkIdInput.Text);

        GD.Print($"Moving to new chunk '{newChunkId}'...");
        await ServerCommunicator.Connection.SendCoreAsync("MoveToChunk", [ServerCommunicator.PlayerName, newChunkId]);

        // Reload scene to join new chunk
        GetTree().ChangeSceneToFile("res://world.tscn");
    }

    private void HandlePlayerAddedToChunk(string playerName) {
        GD.Print("debug HandlePlayerAddedToChunk");

        // TODO: if player already exist, return

        // TODO: get correct position from server
        CreatePlayer(playerName, Vector2.Zero);        
    }

    private void HandlePlayerRemovedFromChunk(string playerName) {
        GD.Print("debug HandlePlayerRemovedFromChunk");

        // TODO: if player doesnt exist, return

        DeletePlayer(playerName);
    }

    private void HandlePlayerMovementUpdate(string playerName, Vector2 newPosition) {
        GD.Print("debug HandlePlayerMovementUpdate");

        Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = (Node3D)playersNode.FindChild(playerName);
        if (playerNode == null) {
            return;
        }

        playerNode.Position = new Vector3(newPosition.X, 0, newPosition.Y);
    }

    private void CreatePlayer(string playerName, Vector2 playerPosition) {
        PackedScene playerScene = GD.Load<PackedScene>("res://Player.tscn");
        // Node playersNode = GetNode<Node>("%Players");
        Node playersNode = GetNode<Node>("/root/World/Players");
        Node3D playerNode = playerScene.Instantiate<Node3D>();
        playerNode.Name = playerName;
        playerNode.Position = new Vector3(
            playerPosition.X,
            0,
            playerPosition.Y
        );
        playerNode.GetNode<Label3D>("%PlayerNameLabel").Text = playerName;
        playersNode.AddChild(playerNode);
        playerNode.Owner = playersNode;
    }

    private void DeletePlayer(string playerName) {
        Node playersNode = GetNode<Node>("%Players");
        Node playerNode = playersNode.FindChild(playerName);

        if (playerNode == null) {
            return;
        }

        // TODO: this doesn't seem to work. The player is still visible
        playerNode.QueueFree();
        playersNode.RemoveChild(playerNode);
    }
}