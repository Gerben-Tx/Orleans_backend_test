using System.Timers;
using Backend.Orleans.SharedContracts;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Backend.Orleans.Silo;

public class TickManager : ITickManager {
    private const int IntervalInMs = 2000;
    private readonly List<Action> _registeredCallbacks = [];
    private readonly Timer _timer;
    private readonly ILogger<TickManager> _logger;

    public TickManager(
        ILogger<TickManager> logger
    ) {
        _logger = logger;
        
        _timer = new Timer(IntervalInMs);
        _timer.Elapsed += Tick;
        _timer.Start();
        
        _logger.LogDebug("Started.");
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

    private void Tick(
        object? sender,
        ElapsedEventArgs elapsedEventArgs
    ) {
        _logger.LogDebug("Ticking...");
        foreach (Action callback in _registeredCallbacks) {
            try {
                callback();
            } catch (Exception e) {
                _logger.LogError(e, "Tick callback threw an exception!");
                throw;
            }
        }
        _logger.LogDebug("Ticked.");
    }

    public void Dispose() {
        _timer.Dispose();
        _registeredCallbacks.Clear();
        
        _logger.LogDebug("Disposed.");
    }
}