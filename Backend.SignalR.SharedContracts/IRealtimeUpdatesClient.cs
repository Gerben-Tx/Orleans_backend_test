namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Server -> Client
/// </summary>
public interface IRealtimeUpdatesClient {
    Task PlayerNewPathCreated(string playerId, int[][] path);
    Task PlayerAddedToChunk(
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        int[][] path
    );
    Task PlayerRemovedFromChunk(string playerId, long chunkId);
    Task Tick();
}