namespace Backend.Orleans.SharedContracts;

public interface IPlayerRegistry : IGrainWithGuidKey {
    public Task AddPlayer(string name, Guid guid);
    public Task RemovePlayer(string name);
    public Task<IPlayerGrain?> FindPlayerByName(string name);
}