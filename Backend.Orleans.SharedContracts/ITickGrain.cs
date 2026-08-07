namespace Backend.Orleans.SharedContracts;

public interface ITickGrain : IGrainWithIntegerKey {
    public const int Key = 0;
    
    public Task<long> GetTicks();
}