namespace Backend.Orleans.SharedContracts.Serialization;

[GenerateSerializer]
public class StreamMessage : IStreamMessage {
    [Id(0)] public string Method { get; init; }
    [Id(1)] public object Data { get; init; }
    [Id(2)] public string GroupName { get; init; }

    public StreamMessage(
        string method,
        object data,
        string chunkId
    ) {
        Method = method;
        Data = data;
        GroupName = chunkId;
    }
}