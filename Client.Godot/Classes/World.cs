using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;

namespace Client.Godot.Classes;

public partial class World : Node3D, IRealtimeUpdatesClient {
    private RandomNumberGenerator _randomNumberGenerator = new RandomNumberGenerator();

    public async override void _Ready() {
        base._Ready();
        
        // Subscribe to realtime updates
        GD.Print("Subscribing to realtime updates...");
        ServerCommunicator.Instance.ClientRegistration(this);

        // Request current chunk id
        GD.Print("Requesting current chunk id...");
        long? currentChunkId =
            await ServerCommunicator.Instance.HubProxy.GetCurrentChunkId(ServerCommunicator.Instance.PlayerName);
        GD.Print($"Current Chunk ID: {currentChunkId}");
        Label chunkLabel = GetNode<Label>("%ChunkLabel");
        chunkLabel.Text = chunkLabel.Text.Replace("{id}", currentChunkId.ToString());

        // Load all players in chunk
        GD.Print("Requesting all players in chunk...");
        List<PlayerListMessage> playersInChunk =
            await ServerCommunicator.Instance.HubProxy.GetPlayersInCurrentChunk(ServerCommunicator.Instance.PlayerName);
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
        await ServerCommunicator.Instance.HubProxy.MoveToChunk(ServerCommunicator.Instance.PlayerName, newChunkId);

        // Reload scene to join new chunk
        GetTree().ChangeSceneToFile("res://world.tscn");
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

    private void UpdatePlayer(string playerName, int posX, int posY) {
        Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = (Node3D)playersNode.FindChild(playerName);
        if (playerNode == null) {
            return;
        }

        playerNode.Position = new Vector3(posX, 0, posY);
    }

    public Task PlayerMovementUpdate(string playerName, int posX, int posY) {
        GD.Print($"debug PlayerMovementUpdate: {playerName}, {posX}, {posY}");

        CallDeferred(nameof(UpdatePlayer), playerName, posX, posY);

        return Task.CompletedTask;
    }

    public Task PlayerAddedToChunk(string playerName) {
        GD.Print($"debug PlayerAddedToChunk: {playerName}");

        // TODO: if player already exist, return

        // TODO: get correct position from server
        CallDeferred(nameof(CreatePlayer), playerName, Vector2.Zero);

        return Task.CompletedTask;
    }

    public Task PlayerRemovedFromChunk(string playerName) {
        GD.Print($"debug PlayerRemovedFromChunk: {playerName}");

        // TODO: if player doesnt exist, return
        CallDeferred(nameof(DeletePlayer), playerName);

        return Task.CompletedTask;
    }
}