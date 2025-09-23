namespace Backend.Orleans.SharedContracts;

public interface IWorldChunkGrain : IGrainWithIntegerKey {
    public Task<List<IPlayerGrain>> GetAllPlayers();
    public Task RemovePlayer(string playerGrainKey, string playerName);
    public Task AddPlayer(string playerGrainKey, string playerName);
    public Task<string> GetRealtimeUpdatesGroupName();
}