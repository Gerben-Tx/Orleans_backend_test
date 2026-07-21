namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Server -> Client
/// </summary>
public interface IRealtimeUpdatesClient {
    Task PlayerMovementUpdate(string playerId, int posX, int posY);
    Task PlayerAddedToChunk(string playerId, string playerName, int posX, int posY);
    Task PlayerRemovedFromChunk(string playerId, long chunkId);
}