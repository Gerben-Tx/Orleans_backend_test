using Azure.Storage.Queues;
using Backend.SignalR.Classes;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddOrleansClient(clientBuilder => {
        clientBuilder.UseLocalhostClustering();
        // clientBuilder.UseSignalR(configure: null);
        clientBuilder.AddAzureQueueStreams(
            name: "StreamProvider",
            configureOptions: options => {
                options.Configure(configureOptions: options => {
                    options.QueueServiceClient = new QueueServiceClient("UseDevelopmentStorage=true");
                });
            }
        );
    })
    // .AddHostedService<OrleansStreamListenerService>()
    .AddSignalR();
// .AddOrleans();
builder.Logging.AddFilter("Backend.SignalR", LogLevel.Debug);

var app = builder.Build();
app.MapHub<RealtimeUpdatesHub>("/realtimeUpdatesHub");
await app.RunAsync();