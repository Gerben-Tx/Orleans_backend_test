namespace Backend.SignalR.SharedContracts;

/// <summary>
/// Contains updates that are send from Orleans -> (Server) -> Client
/// </summary>
public interface IRealtimeUpdatesOrleans {
    Task PlayerMovementUpdate(string groupName, string playerId, int posX, int posY);
    Task PlayerAddedToChunk(string groupName, string playerId, string playerName, int posX, int posY);
    Task PlayerRemovedFromChunk(string groupName, string playerId);
    Task AddToGroupAsync(string groupName, string connectionId);
    Task RemoveFromGroupAsync(string groupName, string connectionId);
}