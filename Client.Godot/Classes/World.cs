using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;
using Godot.Collections;

#if DEBUG
using CommandLine;
using Client.Godot.Classes.Debug;
#endif

namespace Client.Godot.Classes;

public partial class World : Node3D, IRealtimeUpdatesClient {
    private const int ChunkVisibilityRadius = 2;

    private RandomNumberGenerator _randomNumberGenerator = new();
    private long _currentChunkId;
    private WorldInfoMessage _worldInfo;
    private WorldChunkList _loadedChunks = [];
    private PackedScene _tileScene = GD.Load<PackedScene>("res://ground.tscn");
    private Node3D CurrentGroundNode {
        get {
            Node3D ret = GetNodeOrNull<Node3D>(CreateGroundChunkName(_currentChunkId));
            return ret ?? throw new Exception("Current ground node not found!");
        }
    }
    private readonly PlayerList _players = [];
    private ClientSimulation? _clientSimulation = null;

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

        // Get world info
        GD.Print("Requesting world info...");
        _worldInfo = await ServerCommunicator.Instance.HubProxy.GetWorldInfo();
        _clientSimulation = new ClientSimulation(_worldInfo.CurrentTick, HandleTick);
        
        // Request current chunk id
        GD.Print("Requesting current chunk id...");
        _currentChunkId =
            WorldChunk.FromSignalRWorldChunkContract(
                await ServerCommunicator.Instance.HubProxy.GetCurrentChunk(ServerCommunicator.Instance.PlayerName)).ChunkId;
        
        await InitializeChunkAndPlayerData(_currentChunkId);
    }

    public override void _Process(
        double delta
    ) {
        base._Process(delta);

        _clientSimulation?._Process(delta);
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
        // TODO:  System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
        //  This must be handled synchronously, AFTER we unload the chunks
        foreach (WorldChunk chunk in _loadedChunks) {
            List<PlayerListMessage> playersInChunk =
                await ServerCommunicator.Instance.HubProxy.GetPlayersInChunk(
                    ServerCommunicator.Instance.PlayerName,
                    chunk.ChunkId
                );
            GD.Print($"Players in chunk {chunk.ChunkId}: {playersInChunk.Count}");
            foreach (PlayerListMessage playerListMessage in playersInChunk) {
                FindOrCreatePlayer(
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
                    .._loadedChunks.Where(loadedChunk =>
                            !visibleChunks.Select(visibleChunk => visibleChunk.ChunkId).Contains(loadedChunk.ChunkId) // Filter out chunks that are not visible anymore
                            && loadedChunk.ChunkId != _currentChunkId // Filter out the current chunk
                    )
                ];
            
            foreach (WorldChunk chunk in chunksToUnload) {
                GD.Print($"Removing ground chunk {chunk.ChunkId}...");
                chunk.Tiles.ToList().ForEach(tile => tile.QueueFree()); // We MUST remove all references to the tiles before freeing the chunk
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

            // TODO: if we want to use smaller tile sizes (1,1)
            //  we should pre load all (?) the chunks beforehand
            //  There is a lot of stuttering when moving between chunks
            //  when using small tile sizes (<= 5)
            
            for (int x = 0; x < _worldInfo.ChunkSizeX / Tile.TileSize.X; x++) {
                for (int y = 0; y < _worldInfo.ChunkSizeY / Tile.TileSize.Y; y++) {
                    // The chunk position in world coordinates
                    Vector2I chunkWorldPosition = new(
                        worldChunk.X * _worldInfo.ChunkSizeX,
                        worldChunk.Y * _worldInfo.ChunkSizeY
                    );

                    // The ground position in world coordinates
                    Vector2I tiledWorldPosition = new(
                        chunkWorldPosition.X + x,
                        chunkWorldPosition.Y + y
                    );

                    Vector2 tilePositionScaled = new(
                        chunkWorldPosition.X + (x * Tile.TileSize.X),
                        chunkWorldPosition.Y + (y * Tile.TileSize.Y)
                    );
                    
                    MeshInstance3D tileMesh = _tileScene.Instantiate<MeshInstance3D>();
                    tileMesh.Name = CreateGroundChunkName(worldChunk.ChunkId);
                    tileMesh.Position = new Vector3(
                        tilePositionScaled.X,
                        tileMesh.GetAabb().Size.Y,
                        tilePositionScaled.Y
                    );
                    DebugApplyColorToGroundBasedOnChunkId(tileMesh, worldChunk.ChunkId);
                    if (x == _worldInfo.ChunkSizeX / Tile.TileSize.X / 2 && y == _worldInfo.ChunkSizeY / Tile.TileSize.Y / 2) {
                        DebugCreateChunkLabel(tileMesh, worldChunk.ChunkId);
                    }
                    AddChild(tileMesh);

                    Tile groundTile = tileMesh as Node as Tile ?? throw new InvalidOperationException("Ground is not a Tile!");
                    groundTile.WorldPosition = tiledWorldPosition;
                    groundTile.WorldChunk = worldChunk;
                    worldChunk.Tiles.Add(groundTile);
                }
            }
        }
    }

    private static string CreateGroundChunkName(
        long chunkId
    ) => $"GroundChunk{chunkId}";

    /**
     * Applies a color to the ground mesh based on the chunk id.
     * This is just for debugging purposes.
     */
    private void DebugApplyColorToGroundBasedOnChunkId(
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
        ).Darkened(_randomNumberGenerator.Randf() * 30 / 100);
        groundMesh.SetSurfaceOverrideMaterial(0, material);
    }

    /**
     * Creates a label that shows the chunk id.
     * This is just for debugging purposes.
     */
    private void DebugCreateChunkLabel(
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

    private Player FindOrCreatePlayer(
        string playerId,
        string playerName,
        Vector2 playerPosition,
        long chunkId
    ) {
        // if (FindPlayer(playerId) != null) {
        //     return; // Player already exists
        // }
        
        Player? playerObj = _players.Find(x => x.Id == playerId);
        if (playerObj != null) {
            return playerObj;
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

        // TODO: are we sure this wont created duplicates?
        playerObj = new Player { Id = playerId, Path = null };
        _players.Add(playerObj);

        // Hacky way of making sure the correct camera is the "current".
        // This should live in a player script instead.
        if (IsClientPlayer(playerId)) {
            playerNode.GetNode<Camera3D>("Camera3D").Current = true;
        }

        return playerObj;
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
        
        _players.RemoveAll(x => x.Id == playerId);
    }

    // TODO: should this work with the _playerList?
    // TODO: should we connect the player nodes to their Player isntance?
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
        
        GD.Print($"Updating player {playerId} from ({playerNode.Position.X},{playerNode.Position.Y}) to ({posX},{posY})...");

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

    private void HandlePlayerNewPathCreated(
        string playerId,
        Array<Array<int>> path
    ) {
        // Update the path for the player
        // TODO: replace this ugly hack. The playernamer also never gets updated after this...
        Player player = FindOrCreatePlayer(playerId, "Unknown", new Vector2(0, 0), 0);
        player.AddPathFromArray(path);
    }

    private async void HandlePlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        Array<Array<int>> path
    ) {
        Player player = FindOrCreatePlayer(playerId, playerName, new Vector2(posX, posY), chunkId);
        player.AddPathFromArray(path);

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

    private void HandleTick() {
        _players.ForEach(player => {
            Vector2? nextPathPoint = player.GetNextPathPoint();
            if (nextPathPoint == null) {
                return;
            }

            UpdatePlayer(player.Id, (int)nextPathPoint.Value.X, (int)nextPathPoint.Value.Y);
        });
    }

    public Task PlayerNewPathCreated(
        string playerId,
        int[][] path
    ) {
        GD.Print($"debug PlayerNewPathCreated: {playerId}, {path}");
        Array<Array<int>> pathConverted = ConvertPathToArray(path);
        
        // TODO: Move to a queue instead of handling this directly
        CallDeferred(nameof(HandlePlayerNewPathCreated), playerId, pathConverted);
        return Task.CompletedTask;
    }

    public Task PlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        int[][] path
    ) {
        GD.Print($"debug PlayerAddedToChunk {chunkId}: {playerId}, {playerName}, ({posX},{posY}), {path}");
        Array<Array<int>> pathConverted = ConvertPathToArray(path);
        
        // TODO: Move to a queue instead of handling this directly
        CallDeferred(nameof(HandlePlayerAddedToChunk), playerId, playerName, chunkId, posX, posY, pathConverted);
        return Task.CompletedTask;
    }
    
    private static Array<Array<int>> ConvertPathToArray(
        int[][] path
    ) {
        Array<Array<int>> pathConverted = [];
        foreach (int[] pair in path) {
            pathConverted.Add(new Array<int>(pair));
        }

        return pathConverted;
    }

    public Task PlayerRemovedFromChunk(
        string playerId,
        long chunkId
    ) {
        GD.Print($"debug PlayerRemovedFromChunk {chunkId}: {playerId}");
        
        // TODO: Move to a queue instead of handling this directly
        CallDeferred(nameof(HandlePlayerRemovedFromChunk), playerId, chunkId);
        return Task.CompletedTask;
    }
}