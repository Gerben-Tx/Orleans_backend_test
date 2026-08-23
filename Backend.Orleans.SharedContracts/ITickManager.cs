namespace Backend.Orleans.SharedContracts;

public interface ITickManager {
    public void RegisterTickCallback(
        Action tickCallback
    );

    public void UnregisterTickCallback(
        Action tickCallback
    );

    public ulong GetTicks();
    public uint GetTicksPerSecond();
}