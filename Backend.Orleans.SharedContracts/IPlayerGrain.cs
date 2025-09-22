using Backend.Orleans.SharedContracts.Serialization;

namespace Backend.Orleans.SharedContracts;

public interface IPlayerGrain : IGrainWithGuidKey {
    public Task EnterChunk(IWorldChunkGrain chunk);
    public Task<IWorldChunkGrain> GetCurrentChunk();
    public Task Initialize(string connectionId);
    public Task<SerializableVector2> GetPosition();
}