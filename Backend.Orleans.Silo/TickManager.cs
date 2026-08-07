using Backend.Orleans.SharedContracts;
using Microsoft.Extensions.Logging;

namespace Backend.Orleans.Silo;

public class TickManager : ITickManager, IAsyncDisposable {
    private const int Hz = 1; //20; // 20 ticks per second
    private readonly TimeSpan _intervalTimeSpan = new(TimeSpan.TicksPerSecond / Hz);
    private long _ticks;
    private readonly List<Action> _registeredCallbacks = [];
    private readonly PeriodicTimer _timer;
    private readonly ILogger<TickManager> _logger;
    private readonly CancellationTokenSource _stop = new();

    public TickManager(
        ILogger<TickManager> logger
    ) {
        _logger = logger;
        
        _timer = new PeriodicTimer(_intervalTimeSpan);

        StartAsync();
    }

    private async Task StartAsync() {
        try {
            while (await _timer.WaitForNextTickAsync(_stop.Token)) {
                _logger.LogDebug("Tick: {_ticks}", _ticks);
                Tick();
                _ticks++;
            }
        } catch (OperationCanceledException e) {
            // Is this a clean shutdown?
            _logger.LogError(e, "Received OperationCanceledException");
            throw;
        }
    }
    
    public void RegisterTickCallback(
        Action tickCallback
    ) {
        _registeredCallbacks.Add(tickCallback);
        _logger.LogDebug("Registered tick callback.");
    }

    public void UnregisterTickCallback(
        Action tickCallback
    ) {
        _registeredCallbacks.Remove(tickCallback);
        _logger.LogDebug("Unregistered tick callback.");
    }

    public long GetTicks() {
        return _ticks;
    }

    private void Tick() {
        // _logger.LogDebug("Ticking...");
        foreach (Action callback in _registeredCallbacks) {
            try {
                callback();
            } catch (Exception e) {
                _logger.LogError(e, "Tick callback threw an exception!");
                throw;
            }
        }
        // _logger.LogDebug("Ticked.");
    }

    public async ValueTask DisposeAsync() {
        await CastAndDispose(_timer);
        await CastAndDispose(_stop);

        return;

        static async ValueTask CastAndDispose(
            IDisposable resource
        ) {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}