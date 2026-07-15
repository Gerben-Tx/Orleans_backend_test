using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.SharedContracts;

public interface IWorldChunkGrain : IGrainWithIntegerKey {
    public Task<List<IPlayerGrain>> GetAllPlayers();
    public Task RemovePlayer(string playerGrainKey, string playerName);
    public Task AddPlayer(string playerGrainKey, string playerName, SerializableVector2 playerPosition);
    public Task<string> GetRealtimeUpdatesGroupName();
    public Task<long> GetKey();
    public Task<bool> IsPlayerInChunk(string playerGrainKey);
    public Task<WorldChunkNeighbor[]> GetNeighboringChunks(long? chunkId = null);
    public Task<WorldChunkGrainPosition?> GetPositionByChunkId(long? chunkId = null);
    public Task<long?> GetChunkIdByPosition(WorldChunkGrainPosition position);
}