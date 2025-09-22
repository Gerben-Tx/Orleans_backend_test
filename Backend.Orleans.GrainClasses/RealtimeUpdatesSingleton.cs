using Backend.Orleans.GrainClasses.SignalR;
using Backend.SignalR.SharedContracts;
using Microsoft.AspNetCore.SignalR.Client;

namespace Backend.Orleans.GrainClasses;

public sealed class RealtimeUpdatesSingleton {
    public static RealtimeUpdatesSingleton Instance => LAZY.Value;
    private static readonly Lazy<RealtimeUpdatesSingleton> LAZY = new(() => new RealtimeUpdatesSingleton());
    public IRealtimeUpdatesOrleans OrleansProxy { get; private set; }
    private HubConnection Connection { get; set; }

    private RealtimeUpdatesSingleton() {
        Connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5202/realtimeUpdatesHubOrleans")
            .WithAutomaticReconnect()
            .Build();
        OrleansProxy = Connection.ServerProxy<IRealtimeUpdatesOrleans>();
        Connection.StartAsync();
    }
}