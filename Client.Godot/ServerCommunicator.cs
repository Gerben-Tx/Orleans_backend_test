using System.Threading.Tasks;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;

namespace Client.Godot;

public partial class ServerCommunicator : Node {
    public static HubConnection Connection;
    public static string PlayerName;
    public static async Task<HubConnection> ConnectToRealtimeUpdates(string playerName) {
        PlayerName = playerName;
        
        Connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5202/realtimeUpdatesHub")
            .WithAutomaticReconnect()
            .Build();

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
        
        Connection.On<string>("Debug", message => {
            GD.Print($"Received debug message: {message}");
        });

        Connection.On<string>("PlayerAddedToChunk", receivedPlayerName => {
            GD.Print($"Player '{receivedPlayerName}' entered the chunk");
        });
        
        Connection.On<string>("PlayerRemovedFromChunk", receivedPlayerName => {
            GD.Print($"Player '{receivedPlayerName}' left the chunk");
        });
        
        await Connection.StartAsync();
        
        return Connection;
    }
}