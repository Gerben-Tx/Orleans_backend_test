namespace Backend.SignalR.SharedContracts;

public record PlayerPositionMessage {
    public int X { get; init; }
    public int Y { get; init; }
}