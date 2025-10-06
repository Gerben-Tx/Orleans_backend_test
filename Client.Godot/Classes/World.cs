using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Client.Godot.Classes.Debug;
using CommandLine;
using Godot;

namespace Client.Godot.Classes;

public partial class World : Node3D, IRealtimeUpdatesClient {
    private RandomNumberGenerator _randomNumberGenerator = new RandomNumberGenerator();

    public async override void _Ready() {
        base._Ready();

#if DEBUG
        Parser.Default.ParseArguments<DebugCommandLineOptions>(OS.GetCmdlineArgs())
            .WithParsed(o => {
                if (o.Chunk != null) {
                    GD.Print($"Chunk auto join enabled, chunk id: {o.Chunk}");
                    ServerCommunicator.Instance.HubProxy.MoveToChunk(ServerCommunicator.Instance.PlayerName,
                        (int)o.Chunk);
                }
            });
#endif

        // Subscribe to realtime updates
        GD.Print("Subscribing to realtime updates...");
        ServerCommunicator.Instance.ClientRegistration(this);

        // Show player name
        Label playerNameLabel = GetNode<Label>("%PlayerNameLabel");
        playerNameLabel.Text = playerNameLabel.Text.Replace("{name}", ServerCommunicator.Instance.PlayerName);

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
            CreatePlayer(playerListMessage.Id, playerListMessage.Name,
                new Vector2(playerListMessage.PositionX, playerListMessage.PositionY));
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

    private void CreatePlayer(string playerId, string playerName, Vector2 playerPosition) {
        if (FindPlayer(playerId) != null) {
            return; // Player already exists
        }

        PackedScene playerScene = GD.Load<PackedScene>("res://Player.tscn");
        // Node playersNode = GetNode<Node>("%Players");
        Node playersNode = GetNode<Node>("/root/World/Players");
        Node3D playerNode = playerScene.Instantiate<Node3D>();
        playerNode.Name = playerId;
        playerNode.Position = new Vector3(
            playerPosition.X,
            0,
            playerPosition.Y
        );
        playerNode.GetNode<Label3D>("%PlayerNameLabel").Text = playerName;
        playersNode.AddChild(playerNode);
        playerNode.Owner = playersNode;
    }

    private void DeletePlayer(string playerId) {
        Node playerNode = FindPlayer(playerId);
        if (playerNode == null) {
            return;
        }

        playerNode.QueueFree();

        Node playersNode = GetNode<Node>("%Players");
        playersNode.RemoveChild(playerNode);
    }

    private Node FindPlayer(string playerId) {
        Node playersNode = GetNode<Node>("%Players");
        return playersNode.FindChild(playerId);
    }

    private void UpdatePlayer(string playerId, int posX, int posY) {
        Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = (Node3D)playersNode.FindChild(playerId);
        if (playerNode == null) {
            return;
        }

        playerNode.Position = new Vector3(posX, 0, posY);
    }

    private void HandlePlayerMovementUpdate(string playerId, int posX, int posY) {
        UpdatePlayer(playerId, posX, posY);
    }

    private void HandlePlayerAddedToChunk(string playerId, string playerName, int posX, int posY) {
        CreatePlayer(playerId, playerName, new Vector2(posX, posY));
    }

    private void HandlePlayerRemovedFromChunk(string playerId) {
        DeletePlayer(playerId);
    }

    public Task PlayerMovementUpdate(string playerId, int posX, int posY) {
        GD.Print($"debug PlayerMovementUpdate: {playerId}, {posX}, {posY}");
        CallDeferred(nameof(HandlePlayerMovementUpdate), playerId, posX, posY);
        return Task.CompletedTask;
    }

    public Task PlayerAddedToChunk(string playerId, string playerName, int posX, int posY) {
        GD.Print($"debug PlayerAddedToChunk: {playerId}, {playerName}, ({posX},{posY})");
        CallDeferred(nameof(HandlePlayerAddedToChunk), playerId, playerName, posX, posY);
        return Task.CompletedTask;
    }

    public Task PlayerRemovedFromChunk(string playerId) {
        GD.Print($"debug PlayerRemovedFromChunk: {playerId}");
        CallDeferred(nameof(HandlePlayerRemovedFromChunk), playerId);
        return Task.CompletedTask;
    }
}