using Backend.SignalR.SharedContracts;
using Godot;

#if DEBUG
using Client.Godot.Classes.Debug;
using CommandLine;
#endif

namespace Client.Godot.Classes;

public partial class Login : Node3D {
    public override void _Ready() {
        base._Ready();

#if DEBUG
        Parser.Default.ParseArguments<DebugCommandLineOptions>(OS.GetCmdlineArgs())
            .WithParsed(o => {
                if (o.RandomLogin) {
                    GD.Print("RandomLogin enabled");
                }

                if (o.Chunk != null) {
                    GD.Print($"Chunk auto join enabled, chunk id: {o.Chunk}");
                }
            });
#endif

        GD.Randomize();
    }

    private async void _on_login_button_button_up() {
        LineEdit playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        string playerName = playerNameInput.Text;

        IRealtimeUpdatesHub realtimeUpdates = await ServerCommunicator.Instance.ConnectToRealtimeUpdates(playerName);
        await realtimeUpdates.RegisterPlayerGrain(ServerCommunicator.Instance.PlayerName);

        GetTree().ChangeSceneToFile("res://world.tscn");
    }
}