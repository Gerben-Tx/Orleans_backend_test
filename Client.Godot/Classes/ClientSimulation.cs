using System;
using Godot;

namespace Client.Godot.Classes;

public class ClientSimulation {
    private const int TickOffset = 5; // Makes sure we are running in the future // TODO: Explain more, also this should be calculated by the server?
    private long? _ticks;
    private readonly Action _tickHandler;

    public ClientSimulation(
        long ticks,
        Action tickHandler
    ) {
        _ticks = ticks + TickOffset;
        _tickHandler = tickHandler; // TODO: Not sure how to design this yet, for now it is just 1 handler
    }
    
    public void _Process(
        double delta
    ) {
        if (_ticks == null) {
            return;
            // Wait until we have the current server tick before processing anything
        }

        // TODO: Make process loop fixed rate, see: https://www.perplexity.ai/search/033c82f5-befa-46a4-a420-66328b34b186#5

        _tickHandler();
        _ticks++;

        GD.Print($"Ticks: {_ticks}");
    }
}