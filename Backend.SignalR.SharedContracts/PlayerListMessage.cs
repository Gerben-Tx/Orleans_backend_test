namespace Backend.SignalR.SharedContracts;

public class PlayerListMessage {
   public string Id { get; set; }
   public string Name { get; set; }
   public float PositionX { get; set; }
   public float PositionY { get; set; }
}