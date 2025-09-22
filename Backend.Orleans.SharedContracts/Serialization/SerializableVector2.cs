namespace Backend.Orleans.SharedContracts.Serialization;

[GenerateSerializer]
public class SerializableVector2 {
    [Id(0)] public float X { get; set; }

    [Id(1)] public float Y { get; set; }

    public SerializableVector2(float x, float y) {
        X = x;
        Y = y;
    }
}