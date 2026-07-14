namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Client -> Server
/// </summary>
public interface IRealtimeUpdatesHub {
    Task RegisterPlayerGrain(string playerName);

    Task<WorldChunk> GetCurrentChunk(string playerName);

    Task MoveToChunk(string playerName, int newChunkId);

    Task<List<PlayerListMessage>> GetPlayersInChunk(string playerName, long chunkId);

    Task<WorldChunkNeighborsMessage> GetNeighboringChunks(string playerName);
}