using System.Collections.Generic;

namespace Client.Godot.Classes;

public class WorldChunk {
    public readonly long ChunkId;
    public readonly int X;
    public readonly int Y;
    public readonly HashSet<string> PlayerIds;

    private WorldChunk(long chunkId, int x, int y, HashSet<string> playerIds) {
        ChunkId = chunkId;
        X = x;
        Y = y;
        PlayerIds = playerIds;
    }
    
    public static WorldChunk FromSignalRWorldChunkContract(
        Backend.SignalR.SharedContracts.WorldChunkContract signalRWorldChunkContract
    ) {
        return new WorldChunk(
            signalRWorldChunkContract.ChunkId,
            signalRWorldChunkContract.X,
            signalRWorldChunkContract.Y,
            []
        );
    }
};