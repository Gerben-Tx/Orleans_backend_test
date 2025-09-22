namespace Backend.Orleans.SharedContracts;

public interface IStreamMessage {
    public string Method { get; init; }
    public object Data { get; init; }
    public string GroupName { get; init; }
}