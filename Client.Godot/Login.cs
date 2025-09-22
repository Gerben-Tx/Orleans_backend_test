using Godot;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Godot;

public partial class Login : Node3D {
    public override void _Ready() {
        base._Ready();
        
        GD.Randomize();
    }
    
    private async void _on_login_button_button_up() {
        LineEdit playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        string playerName = playerNameInput.Text;
        
        HubConnection realtimeUpdates = await ServerCommunicator.ConnectToRealtimeUpdates(playerName);
        // await _realtimeUpdates.SendCoreAsync("Debug", ["Test from client"]);

        // TODO: Look into strong type signalR client
        //  see https://kristoffer-strube.dk/post/typed-signalr-clients-making-type-safe-real-time-communication-in-dotnet/#:~:text=Source%20generation%20setup

        await realtimeUpdates.SendCoreAsync("RegisterPlayerGrain", [ServerCommunicator.PlayerName]);

        GetTree().ChangeSceneToFile("res://world.tscn");
    }
}