namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Client -> Server
/// </summary>
public interface IRealtimeUpdatesHub {
    Task<string> RegisterPlayerGrain(string playerName);

    Task<WorldChunkContract> GetCurrentChunk(string playerName);

    /// <summary>
    /// Move the player to a new chunk.
    /// This is temporary and will be removed once the player is able to choose its own path.
    /// </summary>
    /// <param name="playerName"></param>
    /// <param name="newChunkId"></param>
    /// <returns></returns>
    Task DebugMoveToChunk(string playerName, int newChunkId);

    Task<List<PlayerListMessage>> GetPlayersInChunk(string playerName, long chunkId);

    Task<VisibleWorldChunksMessage> GetVisibleChunks(string playerName, int radius);
    Task<WorldInfoMessage> GetWorldInfo();
}