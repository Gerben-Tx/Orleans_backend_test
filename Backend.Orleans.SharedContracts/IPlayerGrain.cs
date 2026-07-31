using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.SharedContracts;

public interface IPlayerGrain : IGrainWithGuidKey {
    public Task EnterChunk(IWorldChunkGrain targetChunk);
    public Task<IWorldChunkGrain> GetCurrentChunk();
    public Task Initialize(string connectionId, string playerName);
    public Task<SerializableVector2> GetPosition();
    public Task<string> GetName();
    public Task DeactivateOnIdle();
    public Task<string> GetKey();
    public Task<int> GetChunkVisibilityRadius();
    public void SetChunkVisibilityRadius(int radius);
    public Task DebugMoveToChunk(IWorldChunkGrain chunkGrain);
    public Task OnTickAsync();
}