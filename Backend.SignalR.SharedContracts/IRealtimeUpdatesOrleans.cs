namespace Backend.SignalR.SharedContracts;

public interface IRealtimeUpdatesOrleans {
    Task PlayerMovementUpdate(string groupName, string playerName, int posX, int posY);
    Task PlayerAddedToChunk(string groupName, string playerName);
    Task PlayerRemovedFromChunk(string groupName, string playerName);
    Task AddToGroupAsync(string groupName, string connectionId);
    Task RemoveFromGroupAsync(string groupName, string connectionId);
}