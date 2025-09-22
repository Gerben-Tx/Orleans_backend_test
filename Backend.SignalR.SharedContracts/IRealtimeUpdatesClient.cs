namespace Backend.SignalR.SharedContracts;

public interface IRealtimeUpdatesClient {
    Task PlayerMovementUpdate(string playerName, int posX, int posY);
    Task PlayerAddedToChunk(string playerName);
    Task PlayerRemovedFromChunk(string playerName);
}