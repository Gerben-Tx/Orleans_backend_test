using Backend.Orleans.SharedContracts;

namespace Backend.Orleans.GrainClasses;

internal abstract class PlayerInput {
    public readonly ulong Tick;

    protected PlayerInput(
        ulong tick
    ) {
        Tick = tick;
    }

    public abstract void Apply(
        IPlayerGrain playerGrain
    );
}

internal class MovementInput : PlayerInput {
    private readonly int _destinationX;
    private readonly int _destinationY;

    public MovementInput(
        int destinationX,
        int destinationY,
        ulong tick
    ) : base(tick) {
        _destinationX = destinationX;
        _destinationY = destinationY;
    }

    public override void Apply(
        IPlayerGrain playerGrain
    ) {
        playerGrain.CreateNewPathAndNotify(_destinationX, _destinationY);
    }
}