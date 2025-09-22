namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Server -> Client
/// </summary>
public interface IRealtimeUpdatesClient {
    Task PlayerMovementUpdate(string playerName, int posX, int posY);
    Task PlayerAddedToChunk(string playerName);
    Task PlayerRemovedFromChunk(string playerName);
}