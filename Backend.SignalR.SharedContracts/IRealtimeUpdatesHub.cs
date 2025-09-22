namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Client -> Server
/// </summary>
public interface IRealtimeUpdatesHub {
    Task Debug(string message);

    Task RegisterPlayerGrain(string playerName);

    Task<long?> GetCurrentChunkId(string playerName);

    Task MoveToChunk(string playerName, int newChunkId);

    Task<List<PlayerListMessage>> GetPlayersInCurrentChunk(string playerName);
}