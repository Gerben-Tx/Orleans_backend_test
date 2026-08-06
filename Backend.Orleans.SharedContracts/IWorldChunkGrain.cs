using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.SharedContracts;

public interface IWorldChunkGrain : IGrainWithIntegerKey {
    public const int SizeX = 30;
    public const int SizeY = 30;
    public const int WorldSizeX = 10; // In chunks
    public const int WorldSizeY = 10; // In chunks

    public Task<List<IPlayerGrain>> GetAllPlayers();
    public Task RemovePlayer(string playerGrainKey, string playerName);
    public Task AddPlayer(
        string playerGrainKey,
        string playerName,
        SerializableVector2 playerPosition,
        Queue<SerializableVector2> path
    );
    public Task<string> GetRealtimeUpdatesGroupName();
    public Task<long> GetKey();
    public Task<bool> IsPlayerInChunk(string playerGrainKey);
    public Task<VisibleWorldChunk[]> GetVisibleChunksById(long? chunkId = null);
    public Task<VisibleWorldChunk[]> GetVisibleChunks(int radius);
    public Task<WorldChunkGrainPosition?> GetPosition();
    public Task<WorldChunkGrainPosition?> GetPositionByChunkId(long chunkId);
    public Task<long?> GetChunkIdByPosition(WorldChunkGrainPosition position);
}