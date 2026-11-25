using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Godot;
using Godot.Collections;
using Shared.Pathfinding;

#if DEBUG
using CommandLine;
using Client.Godot.Classes.Debug;
#endif

namespace Client.Godot.Classes;

public partial class World : Node3D, IRealtimeUpdatesClient {
    private const int ChunkVisibilityRadius = 2;
    private readonly PlayerList _players = [];
    private ClientPlayer _clientPlayer = null!;
    private ClientSimulation _clientSimulation = null!;
    private long _currentChunkId;
    private bool _initialized;
    private WorldChunkList _loadedChunks = [];
    private PathfindingService _pathfindingService;

    private RandomNumberGenerator _randomNumberGenerator = new();
    private PackedScene _tileScene = GD.Load<PackedScene>("res://ground.tscn");
    private WorldInfoMessage _worldInfo;

    public Task PlayerNewPathCreated(
        string playerId,
        int[][] path,
        ulong? id
    ) {
        if (!_initialized) {
            return Task.CompletedTask;
        }

        GD.Print(
            $"debug PlayerNewPathCreated: {playerId}, ({string.Join(" | ", path.Select(row => $"[{string.Join(", ", row)}]"))}), {id}");
        Array<Array<int>> pathConverted = ConvertPathToArray(path);

        // TODO: Move to a queue instead of handling this directly
        CallDeferred(
            nameof(HandlePlayerNewPathCreated),
            playerId,
            pathConverted,
            id ?? 0);
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
        if (!_initialized) {
            return Task.CompletedTask;
        }

        GD.Print($"debug PlayerAddedToChunk {chunkId}: {playerId}, {playerName}, ({posX},{posY}), {path}");
        Array<Array<int>> pathConverted = ConvertPathToArray(path);

        // TODO: Move to a queue instead of handling this directly
        CallDeferred(nameof(HandlePlayerAddedToChunk), playerId, playerName, chunkId, posX, posY, pathConverted);
        return Task.CompletedTask;
    }

    public Task PlayerRemovedFromChunk(
        string playerId,
        long chunkId
    ) {
        if (!_initialized) {
            return Task.CompletedTask;
        }

        GD.Print($"debug PlayerRemovedFromChunk {chunkId}: {playerId}");

        // TODO: Move to a queue instead of handling this directly
        CallDeferred(nameof(HandlePlayerRemovedFromChunk), playerId, chunkId);
        return Task.CompletedTask;
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

        // Get world info
        GD.Print("Requesting world info...");
        _worldInfo = await ServerCommunicator.Instance.HubProxy.GetWorldInfo();
        _clientSimulation = new ClientSimulation(_worldInfo.CurrentTick, _worldInfo.TicksPerSecond, HandleTick);
        _pathfindingService = new PathfindingService(
            columns: _worldInfo.WorldSizeX * _worldInfo.ChunkSizeX,
            rows: _worldInfo.WorldSizeY * _worldInfo.ChunkSizeX,
            cellSize: _worldInfo.CellSize
        );
        GD.Print("World info: " + _worldInfo);

        // Request current chunk id
        GD.Print("Requesting current chunk id...");
        _currentChunkId =
            WorldChunk.FromSignalRWorldChunkContract(
                    await ServerCommunicator.Instance.HubProxy.GetCurrentChunk(ServerCommunicator.Instance.PlayerName))
                .ChunkId;

        // TODO: can we instantiate ALL chunks at once, hidden, and only set visible to visible?
        //  This way we don't have to create chunks during runtime (which can be heavy)

        await InitializeChunkAndPlayerData(_currentChunkId);
    }

    public override void _Process(
        double delta
    ) {
        base._Process(delta);

        _clientSimulation?._Process(delta);
    }

    private async Task InitializeChunkAndPlayerData(
        long currentChunkId
    ) {
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
        UpdateChunks(visibleChunks);

        // Load all players in chunk
        GD.Print("Requesting players in all visible chunks...");
        try {
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
        } catch (InvalidOperationException e) {
            GD.PrintErr("TODO: Handle this exception: " + e);
            // TODO:  System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
            //  This must be handled synchronously, AFTER we unload the chunks
        }

        _initialized = true;
    }

    private void UpdateChunkLabel(
        long currentChunkId
    ) {
        Label chunkLabel = GetNode<Label>("%ChunkLabel");
        chunkLabel.Text = $"Chunk ID: {currentChunkId}";
    }

    private void UpdateChunks(
        WorldChunk[] visibleChunks
    ) {
        if (_loadedChunks.Count > 0) {
            // Remove old ground chunks that are no longer visible
            // Create a new array to avoid "System.InvalidOperationException: Collection was modified; enumeration operation may not execute."
            IEnumerable<WorldChunk> chunksToUnload = [
                .. _loadedChunks.Where(loadedChunk =>
                        !visibleChunks.Select(visibleChunk => visibleChunk.ChunkId)
                            .Contains(loadedChunk.ChunkId) // Filter out chunks that are not visible anymore
                        && loadedChunk.ChunkId != _currentChunkId // Filter out the current chunk
                )
            ];

            foreach (WorldChunk chunk in chunksToUnload) {
                GD.Print($"Removing ground chunk {chunk.ChunkId}...");
                // chunk.Tiles.ToList()
                //     .ForEach(tile =>
                //         tile.QueueFree()); // We MUST remove all references to the tiles before freeing the chunk
                // GetNodeOrNull(CreateGroundChunkName(chunk.ChunkId))?.QueueFree();
                Node3D chunkNode = GetNodeOrNull<Node3D>(CreateChunkNodeName(chunk.ChunkId));
                if (chunkNode != null) {
                    chunkNode.Visible = false;
                    chunkNode.ProcessMode = ProcessModeEnum.Disabled; // Disable processing of scripts
                }

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
            if (HasNode(CreateChunkNodeName(worldChunk.ChunkId))) {
                // Enable chunk
                Node3D existingChunkNode = GetNode<Node3D>(CreateChunkNodeName(worldChunk.ChunkId));
                existingChunkNode.Visible = true;
                existingChunkNode.ProcessMode = ProcessModeEnum.Inherit;

                // Ground chunk already exists, skip instantiating
                continue;
            }

            GD.Print(
                $"Instantiating ground chunk {worldChunk.ChunkId} ({worldChunk.X},{worldChunk.Y})...");

            // The chunk position in world coordinates
            Vector2I chunkServerPosition = new(
                worldChunk.X * _worldInfo.ChunkSizeX,
                worldChunk.Y * _worldInfo.ChunkSizeY
            );

            Node3D chunkNode = new();
            chunkNode.Name = CreateChunkNodeName(worldChunk.ChunkId);
            chunkNode.Position = new Vector3(
                chunkServerPosition.X,
                0,
                chunkServerPosition.Y
            );
            AddChild(chunkNode);

            for (int y = 0; y < _worldInfo.ChunkSizeY / _worldInfo.CellSize; y++) {
                for (int x = 0; x < _worldInfo.ChunkSizeX / _worldInfo.CellSize; x++) {
                    Vector2I tileClientPosition = new(
                        chunkServerPosition.X + (x * _worldInfo.CellSize),
                        chunkServerPosition.Y + (y * _worldInfo.CellSize)
                    );

                    MeshInstance3D tileMesh = _tileScene.Instantiate<MeshInstance3D>();
                    tileMesh.Position = chunkNode.ToLocal(
                        new Vector3(
                            tileClientPosition.X,
                            0,
                            tileClientPosition.Y
                        ));
                    DebugApplyColorToGroundBasedOnChunkId(tileMesh, worldChunk.ChunkId);
                    if (x == _worldInfo.ChunkSizeX / _worldInfo.CellSize / 2 &&
                        y == _worldInfo.ChunkSizeY / _worldInfo.CellSize / 2) {
                        DebugCreateChunkLabel(tileMesh, worldChunk.ChunkId);
                    }

                    Tile groundTile = tileMesh as Node as Tile ??
                                      throw new InvalidOperationException("Ground is not a Tile!");
                    groundTile.TileSize = new Vector2I(_worldInfo.CellSize, _worldInfo.CellSize);
                    groundTile.ServerPosition = tileClientPosition;
                    groundTile.WorldChunk = worldChunk;
                    groundTile.OnTileClicked += GroundTileOnOnTileClicked;
#if DEBUG
                    groundTile.DebugCreateLabel();
#endif
                    chunkNode.AddChild(
                        tileMesh); // Make sure to do this after setting up the tile (SetSize() must be called after TileSize is set)
                    worldChunk.Tiles.Add(groundTile);
                }
            }
        }
    }

    private void GroundTileOnOnTileClicked(
        Vector2I position
    ) {
        _clientPlayer.MoveTo(position);
    }

    private static string CreateChunkNodeName(
        long chunkId
    ) => $"Chunk{chunkId}";

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
        chunkLabel.Modulate = new Color(1, 1, 1);
        chunkLabel.OutlineModulate = new Color(0, 0, 0);
        chunkLabel.PixelSize = 0.035f;
        chunkLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
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
        Player? playerObj = _players.Find(x => x.Id == playerId);
        if (playerObj != null) {
            return playerObj;
        }

        GD.Print($"Creating player {playerId} at ({playerPosition.X},{playerPosition.Y}) in chunk {chunkId}...");

        PlayerNode playerNode = Player.CreatePlayerNode(
            playerPosition,
            GetNode<Node>("/root/World/Players"),
            playerId,
            playerName,
            _worldInfo.TicksPerSecond
        );

        playerObj = new Player(playerId, playerName, playerNode);
        if (IsClientPlayer(playerId)) {
            // Convert player to a client player
            _clientPlayer = new ClientPlayer(_clientSimulation, playerObj, _pathfindingService);

            playerObj = _clientPlayer;
        }

        _players.Add(playerObj);

        _loadedChunks
            .FirstOrDefault(x => x.ChunkId == chunkId, null)
            ?.PlayerIds
            .Add(playerId);

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
        Player player,
        int posX,
        int posY
    ) {
        GD.Print(
            $"Updating player {player.Id} from ({player.Position.X},{player.Position.Y}) to ({posX},{posY})...");

        // player.Position = new Vector2I(posX, posY);
        player.UpdatePosition(new Vector2I(posX, posY));
    }

    /// <summary>
    /// Hacky way of determining if a player is the client player.
    /// This should live in a player script instead.
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns>
    /// true if the player is the client player.
    /// </returns>
    private static bool IsClientPlayer(
        string playerId
    ) => playerId == ServerCommunicator.Instance.PlayerId;

    private void HandlePlayerNewPathCreated(
        string playerId,
        Array<Array<int>> path,
        ulong id
    ) {
        // Ugly, but ulong? is not a valid Godot.Variant
        // so the callee (PlayerNewPathCreated) has to convert it to  a non-nullable ulong
        ulong? nullableId = id;
        if (nullableId == 0) {
            nullableId = null;
        }

        // TODO: replace this ugly hack. The player name also never gets updated after this...
        Player player = FindOrCreatePlayer(playerId, "Unknown", new Vector2(0, 0), 0);
        Array<Vector2I> vectorPath = new(path.ToList().ConvertAll(x => new Vector2I(x[0], x[1])));

        // Update the path for the player
        if (IsClientPlayer(playerId)) {
            ClientPlayer clientPlayer = (ClientPlayer)player;
            clientPlayer.ApplyServerReconciliation(vectorPath, nullableId);
        } else {
            player.AddPathFromArray(vectorPath);
        }
    }

    private async void HandlePlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        Array<Array<int>> path
    ) {
        if (!IsClientPlayer(playerId)) {
            // Update paths for other players
            // Don't do this for the client player, since it already has it's own local
            // version of the path. If we did this it would override the local path
            // and the client player would teleport back.
            Player player = FindOrCreatePlayer(playerId, playerName, new Vector2(posX, posY), chunkId);
            player.AddPathFromArray(path);
        } else {
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
# if DEBUG
        // Show players server position
        // so we can match it to the client position
        foreach (Player player in _players) {
            if (!DisplayServer.WindowCanDraw()) {
                // Ignore headless debug clients
                continue;
            }

            PlayerPositionMessage? debugPlayerPositionMessage = Task.Run(() =>
                    ServerCommunicator.Instance.HubProxy.DebugGetPlayerPosition(player.Name))
                .GetAwaiter().GetResult();
            // GD.Print($"Player position: {debugPlayerPositionMessage?.X}, {debugPlayerPositionMessage?.Y}");
            if (debugPlayerPositionMessage != null) {
                DebugDraw3D.DrawCapsule(
                    new Vector3(debugPlayerPositionMessage.X, 1, debugPlayerPositionMessage.Y),
                    Quaternion.Identity,
                    0.5f,
                    2.0f,
                    new Color(0, 0, 0.7f),
                    1.0f);
            }
        }

        // Show world info
        WorldInfoMessage debugWorldInfoMessage = Task.Run(() => ServerCommunicator.Instance.HubProxy.GetWorldInfo())
            .GetAwaiter().GetResult();
        GetNode<Label>("%ServerTickLabel").Text = $"Server Tick: {debugWorldInfoMessage.CurrentTick}";
        GetNode<Label>("%ClientTickLabel").Text = $"Client Tick: {_clientSimulation.Ticks}";
        GetNode<Label>("%DifferenceTickLabel").Text =
            $"Difference: {_clientSimulation.Ticks - debugWorldInfoMessage.CurrentTick}";
# endif

        // Synchronize the client tick with the server tick periodically.
        // This prevents the client from being out of sync with the server
        // and never catching up.
        if (_clientSimulation.Ticks % 100 == 0) {
            WorldInfoMessage worldInfoMessage = Task.Run(() => ServerCommunicator.Instance.HubProxy.GetWorldInfo())
                .GetAwaiter().GetResult();
            _clientSimulation.SynchronizeTicks(worldInfoMessage.CurrentTick);
        }

        _players.ForEach(player => {
            (player as ClientPlayer)?.HandleTick(_worldInfo);

            Vector2? nextPathPoint = player.GetNextPathPoint();
            if (nextPathPoint == null) {
                return;
            }

            UpdatePlayer(player, (int)nextPathPoint.Value.X, (int)nextPathPoint.Value.Y);
        });
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
}