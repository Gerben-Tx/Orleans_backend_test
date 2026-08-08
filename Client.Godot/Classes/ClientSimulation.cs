using System;
using Godot;

namespace Client.Godot.Classes;

public class ClientSimulation {
    /// <summary>
    /// The offset between the server ticks and the client ticks. This is needed to make sure the client is always running in the future.
    /// This accounts for potentially laggy clients.
    /// // TODO: this should be calculated by the server?
    /// </summary>
    private const int TickOffset = 5;

    /// <summary>
    /// Maximum number of simulation steps per frame.
    /// If needed, we can perform multiple simulation steps per frame
    /// (for example, when we accumulated multiple ticks due to a lagging process).
    /// </summary>
    private const int MaxSimulationStepsPerFrame = 5;

    /// <summary>
    /// The time between ticks in seconds. Should be fetched from the server.
    /// </summary>
    private readonly double _tickDurationSeconds;
    private readonly Action _tickHandler;

    /// <summary>
    /// Accumulates the time between frames and is decremented on each frame by the <see cref="_tickDurationSeconds"/>
    /// </summary>
    private double _accumulator;
    private ulong? _ticks;

    public ClientSimulation(
        ulong ticks,
        uint serverTicksPerSecond,
        Action tickHandler
    ) {
        _ticks = ticks + TickOffset;
        _tickHandler = tickHandler; // TODO: Not sure how to design this yet, for now it is just 1 handler
        _tickDurationSeconds = 1.0 / serverTicksPerSecond;
    }

    public void _Process(
        double delta
    ) {
        if (_tickDurationSeconds <= 0) {
            return;
        }

        // Prevent a pause / debugger break from accumulating
        // tons of simulation steps
        // NOTE: Not sure if 0.25 is a good value, need to test more
        delta = Math.Min(delta, 0.25);
        _accumulator += delta;

        // Handle each tick we've accumulated.
        // If everything is running perfectly, the steps should be 1
        // (meaning we have accumulated only 1 tick)
        int steps = 0;
        while (_accumulator >= _tickDurationSeconds && steps < MaxSimulationStepsPerFrame) {
            // Subtract one tick from the accumulator
            _accumulator -= _tickDurationSeconds;

            // Handle the tick
            _tickHandler();

            // Increment counters
            _ticks++;
            steps++;

            GD.Print($"Tick: {_ticks}, Step: {steps}");
        }
    }
}