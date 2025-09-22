namespace Backend.Orleans.SharedContracts;

public interface IWorldChunkGrain : IGrainWithIntegerKey {
    public Task<List<IPlayerGrain>> GetAllPlayers();
    public Task RemovePlayer(Guid playerGrainKey, string playerName);
    public Task AddPlayer(Guid playerGrainKey, string playerName);
    public Task<string> GetRealtimeUpdatesGroupName();
}