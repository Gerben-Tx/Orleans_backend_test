using System.Numerics;

namespace Backend.Orleans.SharedContracts.Serialization;

[GenerateSerializer]
public class SerializableVector2 {
    [Id(0)] public int X { get; set; }

    [Id(1)] public int Y { get; set; }

    public SerializableVector2(
        int x,
        int y
    ) {
        X = x;
        Y = y;
    }

    public Vector2 ToVector2() => new(X, Y);
}