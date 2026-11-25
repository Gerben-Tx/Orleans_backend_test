using Azure.Storage.Queues;
using Backend.SignalR.Classes;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddOrleansClient(clientBuilder => {
        clientBuilder.UseLocalhostClustering(
            gatewayPort: 30010
        );
        // clientBuilder.UseSignalR(configure: null);
        clientBuilder.AddAzureQueueStreams(
            name: "StreamProvider",
            configureOptions: options => {
                options.Configure(
                    configureOptions: options => {
                        options.QueueServiceClient = new QueueServiceClient("UseDevelopmentStorage=true");
                    });
            }
        );
    })
    // .AddHostedService<OrleansStreamListenerService>()
    .AddSignalR();
// .AddOrleans();
builder.Logging.AddFilter("Backend.SignalR", LogLevel.Debug);
builder.Logging.AddSimpleConsole(options => {
    options.IncludeScopes = true;
    options.TimestampFormat = "[HH:mm:ss.fff zzz] ";
    options.SingleLine = true;
});

var app = builder.Build();
app.MapHub<RealtimeUpdatesHubClient>("/realtimeUpdatesHubClient");
app.MapHub<RealtimeUpdatesHubOrleans>("/realtimeUpdatesHubOrleans");
await app.RunAsync();