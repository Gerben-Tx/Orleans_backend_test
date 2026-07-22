using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;

#if DEBUG
using CommandLine;
using Client.Godot.Classes.Debug;
#endif

namespace Client.Godot.Classes;

public partial class World : Node3D, IRealtimeUpdatesClient {
    private const int ChunkVisibilityRadius = 2;

    private RandomNumberGenerator _randomNumberGenerator = new();
    private long _currentChunkId;
    private WorldChunkList _loadedChunks = [];
    private Node3D CurrentGroundNode {
        get {
            Node3D ret = GetNodeOrNull<Node3D>(CreateGroundChunkName(_currentChunkId));
            return ret ?? throw new Exception("Current ground node not found!");
        }
    }

    public async override void _Ready() {
        base._Ready();

#if DEBUG
        Parser.Default.ParseArguments<DebugCommandLineOptions>(OS.GetCmdlineArgs())
            .WithParsed(o => {
                if (o.Chunk != null) {
                    GD.Print($"Chunk auto join enabled, chunk id: {o.Chunk}");
                    ServerCommunicator.Instance.HubProxy.DebugMoveToChunk(
                        ServerCommunicator.Instance.PlayerName,
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
        _currentChunkId =
            WorldChunk.FromSignalRWorldChunkContract(
                await ServerCommunicator.Instance.HubProxy.GetCurrentChunk(ServerCommunicator.Instance.PlayerName)).ChunkId;
        
        await InitializeChunkAndPlayerData(_currentChunkId);
    }

    private async Task InitializeChunkAndPlayerData(long currentChunkId) {
        GD.Print($"Current Chunk ID: {currentChunkId}");
        UpdateChunkLabel(currentChunkId);

        // Get visible chunks
        GD.Print("Requesting visible chunks...");
        VisibleWorldChunksMessage chunksMessage =
            await ServerCommunicator.Instance.HubProxy.GetVisibleChunks(
                ServerCommunicator.Instance.PlayerName,
                ChunkVisibilityRadius);
        GD.Print(
            $"Visible Chunks: {string.Join(",", chunksMessage.Chunks.Select(chunk => $"(id: {chunk.ChunkId}, x: {chunk.X}, y: {chunk.Y})"))}");
        WorldChunk[] visibleChunks = chunksMessage.Chunks.ToList()
            .ConvertAll(WorldChunk.FromSignalRWorldChunkContract)
            .ToArray();
        InstantiateGroundChunks(visibleChunks);

        // Load all players in chunk
        GD.Print("Requesting players in all visible chunks...");
        foreach (WorldChunk chunk in _loadedChunks) {
            List<PlayerListMessage> playersInChunk =
                await ServerCommunicator.Instance.HubProxy.GetPlayersInChunk(
                    ServerCommunicator.Instance.PlayerName,
                    chunk.ChunkId
                );
            GD.Print($"Players in chunk {chunk.ChunkId}: {playersInChunk.Count}");
            foreach (PlayerListMessage playerListMessage in playersInChunk) {
                CreatePlayer(
                    playerListMessage.Id,
                    playerListMessage.Name,
                    new Vector2(playerListMessage.PositionX, playerListMessage.PositionY),
                    chunk.ChunkId
                );
            }
        }
    }

    private void UpdateChunkLabel(
        long currentChunkId
    ) {
        Label chunkLabel = GetNode<Label>("%ChunkLabel");
        chunkLabel.Text = $"Chunk ID: {currentChunkId}";
    }

    private void InstantiateGroundChunks(
        WorldChunk[] visibleChunks
    ) {
        if (_loadedChunks.Count > 0) {
            // Remove old ground chunks that are no longer visible
            // Create a new array to avoid "System.InvalidOperationException: Collection was modified; enumeration operation may not execute."
            IEnumerable<WorldChunk> chunksToUnload =
                [
                    .._loadedChunks.Where(x =>
                            !visibleChunks.Select(y => y.ChunkId).Contains(x.ChunkId) // Filter out chunks that are not visible anymore
                            && x.ChunkId != _currentChunkId // Filter out the current chunk
                    )
                ];
            
            foreach (WorldChunk chunk in chunksToUnload) {
                GD.Print($"Removing ground chunk {chunk.ChunkId}...");
                GetNodeOrNull(CreateGroundChunkName(chunk.ChunkId))?.QueueFree();
                _loadedChunks.Remove(chunk);

                // Remove players that were part of this chunk
                foreach (string playerId in chunk.PlayerIds.ToArray()) {
                    GD.Print($"Removing player {playerId} from chunk {chunk.ChunkId}...");
                    DeletePlayer(playerId);
                }
            }
        }

        // Update loaded chunks list
        visibleChunks.ToList().ForEach(visibleChunk => _loadedChunks.Add(visibleChunk));

        foreach (WorldChunk worldChunk in _loadedChunks) {
            if (HasNode(CreateGroundChunkName(worldChunk.ChunkId))) {
                // Ground chunk already exists, skip
                continue;
            }

            GD.Print(
                $"Instantiating ground chunk {worldChunk.ChunkId} ({worldChunk.X},{worldChunk.Y})...");

            MeshInstance3D ground = GD.Load<PackedScene>("res://ground.tscn").Instantiate<MeshInstance3D>();
            ground.Name = CreateGroundChunkName(worldChunk.ChunkId);
            ground.Position = new Vector3(
                worldChunk.X * ground.GetAabb().Size.X,
                ground.GetAabb().Size.Y,
                worldChunk.Y * ground.GetAabb().Size.Z
            );
            ApplyColorToGroundBasedOnChunkId(ground, worldChunk.ChunkId);
            CreateChunkLabel(ground, worldChunk.ChunkId);
            AddChild(ground);
        }
    }

    private static string CreateGroundChunkName(
        long chunkId
    ) => $"GroundChunk{chunkId}";

    /**
     * Applies a color to the ground mesh based on the chunk id.
     * This is just for debugging purposes.
     */
    private void ApplyColorToGroundBasedOnChunkId(
        MeshInstance3D groundMesh,
        long chunkId
    ) {
        StandardMaterial3D material = new();
        uint hash = unchecked((uint)chunkId);
        hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
        hash = ((hash >> 16) ^ hash) * 0x45d9f3b;
        hash = (hash >> 16) ^ hash;

        material.AlbedoColor = new Color(
            ((hash >> 16) & 0xFF) / 255.0f,
            ((hash >> 8) & 0xFF) / 255.0f,
            (hash & 0xFF) / 255.0f
        );
        groundMesh.SetSurfaceOverrideMaterial(0, material);
    }

    private void CreateChunkLabel(
        MeshInstance3D groundMesh,
        long chunkId
    ) {
        Label3D chunkLabel = new();
        chunkLabel.Text = chunkId.ToString();
        chunkLabel.Position = new Vector3(0, 2, 0);
        chunkLabel.FontSize = 16;
        chunkLabel.Modulate = new Color(1, 1, 1);
        chunkLabel.OutlineModulate = new Color(0, 0, 0);
        chunkLabel.OutlineSize = 8;
        chunkLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        chunkLabel.FixedSize = true;
        groundMesh.AddChild(chunkLabel);
    }

    public async void _on_go_to_chunk_button_button_up() {
        GD.Print("_on_go_to_chunk_button_button_up");

        LineEdit newChunkIdInput = GetNode<LineEdit>("%NewChunkIdInput");
        int newChunkId = int.Parse(newChunkIdInput.Text);

        GD.Print($"Moving to new chunk '{newChunkId}'...");
        await ServerCommunicator.Instance.HubProxy.DebugMoveToChunk(ServerCommunicator.Instance.PlayerName, newChunkId);

        // Reload scene to join new chunk
        // GetTree().ChangeSceneToFile("res://world.tscn");
        GetTree().ReloadCurrentScene();
    }

    private void CreatePlayer(
        string playerId,
        string playerName,
        Vector2 playerPosition,
        long chunkId
    ) {
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

        _loadedChunks
            .FirstOrDefault(x => x.ChunkId == chunkId, null)
            ?.PlayerIds
            .Add(playerId);

        // Hacky way of making sure the correct camera is the "current".
        // This should live in a player script instead.
        if (IsClientPlayer(playerId)) {
            playerNode.GetNode<Camera3D>("Camera3D").Current = true;
        }
    }

    private void DeletePlayer(
        string playerId
    ) {
        Node? playerNode = FindPlayer(playerId);
        if (playerNode == null) {
            return;
        }

        if (IsClientPlayer(playerId)) {
            // Don't delete the client player
            return;
        }

        playerNode.QueueFree();

        Node playersNode = GetNode<Node>("%Players");
        playersNode.RemoveChild(playerNode);

        _loadedChunks
            .FirstOrDefault(x => x.PlayerIds.Contains(playerId), null)
            ?.PlayerIds
            .Remove(playerId);
    }

    private Node? FindPlayer(
        string playerId
    ) {
        Node playersNode = GetNode<Node>("%Players");
        return playersNode.GetNodeOrNull(playerId);
    }

    private void UpdatePlayer(
        string playerId,
        int posX,
        int posY
    ) {
        Node playersNode = GetNode<Node>("%Players");
        Node3D playerNode = (Node3D)playersNode.FindChild(playerId);
        if (playerNode == null) {
            return;
        }

        Aabb groundAabb = ((MeshInstance3D)CurrentGroundNode).GetAabb();
        playerNode.Position = new Vector3(
            posX - (groundAabb.Size.X / 2),
            0,
            posY - (groundAabb.Size.Z / 2)
        );
    }

    /// <summary>
    /// Hacky way of determining if a player is the client player.
    /// This should live in a player script instead.
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns>
    /// true if the player is the client player.
    /// </returns>
    private static bool IsClientPlayer(string playerId) => playerId == ServerCommunicator.Instance.PlayerId;

    private void HandlePlayerMovementUpdate(
        string playerId,
        int posX,
        int posY
    ) {
        UpdatePlayer(playerId, posX, posY);
    }

    private async void HandlePlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY
    ) {
        CreatePlayer(playerId, playerName, new Vector2(posX, posY), chunkId);

        if (IsClientPlayer(playerId)) {
            // Re-initialize chunk and player data if the client player joined a new chunk
            _currentChunkId = chunkId;
            await InitializeChunkAndPlayerData(chunkId);
        }
    }

    private void HandlePlayerRemovedFromChunk(
        string playerId,
        long chunkId
    ) {
        DeletePlayer(playerId);
    }

    public Task PlayerMovementUpdate(
        string playerId,
        int posX,
        int posY
    ) {
        GD.Print($"debug PlayerMovementUpdate: {playerId}, {posX}, {posY}");
        CallDeferred(nameof(HandlePlayerMovementUpdate), playerId, posX, posY);
        return Task.CompletedTask;
    }

    public Task PlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY
    ) {
        GD.Print($"debug PlayerAddedToChunk {chunkId}: {playerId}, {playerName}, ({posX},{posY})");
        CallDeferred(nameof(HandlePlayerAddedToChunk), playerId, playerName, chunkId, posX, posY);
        return Task.CompletedTask;
    }

    public Task PlayerRemovedFromChunk(
        string playerId,
        long chunkId
    ) {
        GD.Print($"debug PlayerRemovedFromChunk {chunkId}: {playerId}");
        CallDeferred(nameof(HandlePlayerRemovedFromChunk), playerId, chunkId);
        return Task.CompletedTask;
    }
}