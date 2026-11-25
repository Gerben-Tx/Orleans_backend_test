using Godot;

namespace Client.Godot.Classes;

public partial class PlayerNode : Node3D {
    private const float ArrivalDistance = 0.001F;
    private float _movementSpeed;

    private Vector2I _newPosition;

    private int _tempCounter = 0;
    private int _ticksPerSecond = 0;
    private Vector2I Position2I => new((int)Position.X, (int)Position.Z);

    public void SetTicksPerSecond(
        uint ticksPerSecond
    ) {
        _ticksPerSecond = (int)ticksPerSecond;
    }

    public void UpdatePosition(
        Vector2I position
    ) {
        _newPosition = position;

        Vector3 targetPosition = new(_newPosition.X, Position.Y, _newPosition.Y);
        float distanceToTarget = Position.DistanceTo(targetPosition);

        // Calculate movement speed based on distance to target and ticks per second
        // this way it always takes 1 tick to go to the target position (which should be 1 tile away)
        _movementSpeed = distanceToTarget * _ticksPerSecond;
    }

    public override void _Process(
        double delta
    ) {
        if (_newPosition == Vector2I.Zero) {
            return; // Ignore un-initialized new pos
        }

        Vector3 targetPosition = new(_newPosition.X, Position.Y, _newPosition.Y);

        if (Position.DistanceTo(targetPosition) > ArrivalDistance) {
            // GD.Print(
            //     $"[PlayerNode._Process] ({_tempCounter}) Position2I: {Position2I}, _newPosition: {_newPosition}, Position: {Position}");

            // Smoothly move towards the target position
            Position = Position.MoveToward(
                targetPosition,
                _movementSpeed * (float)delta);
        } else {
            Position = targetPosition; // Snap to the target position if we are close enough
        }
    }
}