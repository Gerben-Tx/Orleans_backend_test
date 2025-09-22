using Backend.SignalR.SharedContracts;
using Godot;

namespace Client.Godot;

public partial class Login : Node3D {
    public override void _Ready() {
        base._Ready();
        
        GD.Randomize();
    }
    
    private async void _on_login_button_button_up() {
        LineEdit playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        string playerName = playerNameInput.Text;
        
        IRealtimeUpdatesHub realtimeUpdates = await ServerCommunicator.Instance.ConnectToRealtimeUpdates(playerName);
        // await _realtimeUpdates.SendCoreAsync("Debug", ["Test from client"]);

        // await realtimeUpdates.SendCoreAsync("RegisterPlayerGrain", [ServerCommunicator.PlayerName]);
        await realtimeUpdates.RegisterPlayerGrain(ServerCommunicator.Instance.PlayerName); // TODO: test if this works

        GetTree().ChangeSceneToFile("res://world.tscn");
    }
}