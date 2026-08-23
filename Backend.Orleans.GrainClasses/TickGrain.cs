using Backend.Orleans.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class TickGrain : BaseGrain, ITickGrain {
    private readonly ITickManager _tickManager;

    public TickGrain(
        ILogger<BaseGrain> logger,
        ITickManager tickManager
    ) : base(logger) {
        _tickManager = tickManager;
    }

    public Task<ulong> GetTicks() {
        return Task.FromResult(_tickManager.GetTicks());
    }

    public Task<uint> GetTicksPerSecond() {
        return Task.FromResult(_tickManager.GetTicksPerSecond());
    }
}