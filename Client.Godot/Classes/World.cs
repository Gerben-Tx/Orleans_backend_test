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
    
    private RandomNumberGenerator _randomNumberGenerator = new RandomNumberGenerator();
    private MeshInstance3D _groundNode;
    private WorldChunk _currentChunk;

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

        _groundNode = GetNode<MeshInstance3D>("%Ground");
        
        // Subscribe to realtime updates
        GD.Print("Subscribing to realtime updates...");
        ServerCommunicator.Instance.ClientRegistration(this);

        // Show player name
        Label playerNameLabel = GetNode<Label>("%PlayerNameLabel");
        playerNameLabel.Text = playerNameLabel.Text.Replace("{name}", ServerCommunicator.Instance.PlayerName);

        // Request current chunk id
        GD.Print("Requesting current chunk id...");
        _currentChunk =
            await ServerCommunicator.Instance.HubProxy.GetCurrentChunk(ServerCommunicator.Instance.PlayerName);
        long currentChunkId = _currentChunk.ChunkId;
        GD.Print($"Current Chunk ID: {currentChunkId}");
        UpdateChunkLabel(currentChunkId);

        // Get neighboring chunks
        GD.Print("Requesting neighboring chunks...");
        WorldChunkNeighborsMessage chunkNeighborsMessage =
            await ServerCommunicator.Instance.HubProxy.GetNeighboringChunks(ServerCommunicator.Instance.PlayerName, ChunkVisibilityRadius);
        GD.Print(
            $"Neighboring Chunks: {string.Join(",", chunkNeighborsMessage.Chunks.Select(chunk => $"(id: {chunk.ChunkId}, x: {chunk.X}, y: {chunk.Y})"))}");
        InstantiateGroundChunks(chunkNeighborsMessage.Chunks, _currentChunk);

        // Load all players in chunk
        GD.Print("Requesting players in all visible chunks...");
        List<WorldChunk> allChunksList = new(chunkNeighborsMessage.Chunks);
        allChunksList.Add(_currentChunk);
        WorldChunk[] allChunks = allChunksList.ToArray();
        foreach (WorldChunk chunk in allChunks) {
            if (chunk == null) {
                continue;
            }
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
                    new Vector2(playerListMessage.PositionX, playerListMessage.PositionY));
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
        WorldChunk[] neighbors,
        WorldChunk currentChunk
    ) {
        Aabb groundAabb = _groundNode.Mesh.GetAabb();
        
        foreach (WorldChunk worldChunkNeighbor in neighbors) {
            if (worldChunkNeighbor == null) {
                continue;
            }
            
            GD.Print($"Instantiating ground chunk {worldChunkNeighbor.ChunkId} ({worldChunkNeighbor.X},{worldChunkNeighbor.Y})...");
            
            MeshInstance3D ground = (MeshInstance3D)_groundNode.Duplicate();
            ground.Position = new Vector3(
                worldChunkNeighbor.X * groundAabb.Size.X,
                groundAabb.Size.Y,
                worldChunkNeighbor.Y * groundAabb.Size.Z
            );
            ApplyColorToGroundBasedOnChunkId(ground, worldChunkNeighbor.ChunkId);
            CreateChunkLabel(ground, worldChunkNeighbor.ChunkId);
            AddChild(ground);
        }
        
        // Center ground node
        _groundNode.Position = new Vector3(
            currentChunk.X * groundAabb.Size.X,
            groundAabb.Size.Y,
            currentChunk.Y * groundAabb.Size.Z
        );
        ApplyColorToGroundBasedOnChunkId(_groundNode, currentChunk.ChunkId);
        CreateChunkLabel(_groundNode, currentChunk.ChunkId);
    }

    /**
     * Applies a color to the ground mesh based on the chunk id.
     * This is just for debugging purposes.
     */
    private void ApplyColorToGroundBasedOnChunkId(
        MeshInstance3D groundMesh,
        long chunkId
    ) {
        StandardMaterial3D material = new StandardMaterial3D();
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
        Label3D chunkLabel = new Label3D();
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
        Vector2 playerPosition
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
        
        // Hacky way of making sure the correct camera is the "current".
        // This should live in a player script instead.
        if (IsClientPlayer(playerNode)) {
            playerNode.GetNode<Camera3D>("Camera3D").Current = true;
        }
    }

    private void DeletePlayer(
        string playerId
    ) {
        Node playerNode = FindPlayer(playerId);
        if (playerNode == null) {
            return;
        }

        if (IsClientPlayer((Node3D)playerNode)) {
            // Don't delete the client player
            return;
        }

        playerNode.QueueFree();

        Node playersNode = GetNode<Node>("%Players");
        playersNode.RemoveChild(playerNode);
    }

    private Node FindPlayer(
        string playerId
    ) {
        Node playersNode = GetNode<Node>("%Players");
        return playersNode.FindChild(playerId);
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

        Aabb groundAabb = _groundNode.Mesh.GetAabb();
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
    /// <param name="playerNode"></param>
    /// <returns>
    /// true if the player is the client player.
    /// </returns>
    private static bool IsClientPlayer(
        Node3D playerNode
    ) {
        return playerNode.GetNode<Label3D>("%PlayerNameLabel").Text == ServerCommunicator.Instance.PlayerName;
    }

    private void HandlePlayerMovementUpdate(
        string playerId,
        int posX,
        int posY
    ) {
        UpdatePlayer(playerId, posX, posY);
    }

    private void HandlePlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY
    ) {
        CreatePlayer(playerId, playerName, new Vector2(posX, posY));
        
        // Update the current chunk label
        if (IsClientPlayer((Node3D)FindPlayer(playerId))) {
            UpdateChunkLabel(chunkId);
        }
    }

    private void HandlePlayerRemovedFromChunk(
        string playerId
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
        CallDeferred(nameof(HandlePlayerRemovedFromChunk), playerId);
        return Task.CompletedTask;
    }
}