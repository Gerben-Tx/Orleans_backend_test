namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Orleans -> (Server) -> Client
/// </summary>
public interface IRealtimeUpdatesOrleans {
    Task PlayerNewPathCreated(string groupName, string playerId, int[][] path);
    Task PlayerAddedToChunk(
        string groupName,
        string playerId,
        string playerName,
        long chunkId,
        int posX,
        int posY,
        int[][] path
    );
    Task PlayerRemovedFromChunk(string groupName, string playerId, long chunkId);
    Task AddToGroupAsync(string groupName, string connectionId);
    Task RemoveFromGroupAsync(string groupName, string connectionId);
    Task Tick(string groupName);
}