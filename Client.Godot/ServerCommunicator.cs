using System.Threading.Tasks;
using Backend.SignalR.SharedContracts;
using Client.Godot.SignalR;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Godot;

public partial class ServerCommunicator : Node {
    public static ServerCommunicator Instance { get; private set; }
    private HubConnection Connection { get; set; }
    public IRealtimeUpdatesHub HubProxy { get; set; }
    public string PlayerName { get; private set; }

    public override void _Ready() {
        base._Ready();

        Instance = this;
    }

    public void ClientRegistration(IRealtimeUpdatesClient client) {
        // ReSharper disable RedundantTypeArgumentsOfMethod This is necessary for
        // Microsoft.AspNetCore.SignalR.Client.SourceGenerator to work properly.
        // We need explicit presence of the generic type argument.
        Connection.ClientRegistration<IRealtimeUpdatesClient>(client);
    }

    public async Task<IRealtimeUpdatesHub> ConnectToRealtimeUpdates(string playerName) {
        PlayerName = playerName;

        Connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5202/realtimeUpdatesHubClient")
            .WithAutomaticReconnect()
            .Build();
        HubProxy = Connection.ServerProxy<IRealtimeUpdatesHub>();

        Connection.Reconnecting += error => {
            GD.Print($"Connection lost to the realtime updates server, reconnecting... ({error})");
            return Task.CompletedTask;
        };

        Connection.Reconnected += _ => {
            GD.Print("Connection reconnected");
            return Task.CompletedTask;
        };

        Connection.Closed += error => {
            if (error != null) {
                GD.Print($"Connection closed ({error})");
            } else {
                GD.Print($"Connection closed");
            }

            return Task.CompletedTask;
        };

        await Connection.StartAsync();

        return HubProxy;
    }
}