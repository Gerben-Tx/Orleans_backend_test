using System.Linq;
using Backend.SignalR.SharedContracts;
using Godot;
using RandomFriendlyNameGenerator;

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
                    string playerName = NameGenerator.Identifiers.Get(
                            1,
                            IdentifierComponents.FirstName | IdentifierComponents.Adjective |
                            IdentifierComponents.Animal,
                            NameOrderingStyle.SilentBobStyle, "_", true, 16)
                        .First();
                    GD.Print($"RandomLogin enabled. Player name: '{playerName}'");

                    DoLogin(playerName);
                } else if (o.LoginName != null) {
                    GD.Print($"LoginName enabled. Player name: '{o.LoginName}'");

                    DoLogin(o.LoginName);
                }
            });
#endif

        GD.Randomize();
    }

    private void _on_login_button_button_up() {
        LineEdit playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        string playerName = playerNameInput.Text;

        DoLogin(playerName);
    }

    private async void DoLogin(string playerName) {
        IRealtimeUpdatesHub realtimeUpdates = await ServerCommunicator.Instance.ConnectToRealtimeUpdates(playerName);
        await realtimeUpdates.RegisterPlayerGrain(ServerCommunicator.Instance.PlayerName);

        GetTree().ChangeSceneToFile("res://world.tscn");
    }
}