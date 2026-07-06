using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Backend.Orleans.SharedContracts;
using Backend.Orleans.Silo.Pathfinding;
using Backend.Orleans.Silo.SignalR;
using Backend.SignalR.SharedContracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;

using IHost host = new HostBuilder()
    .UseOrleans(silo => {
        silo
            .UseLocalhostClustering()
            .AddAzureTableGrainStorage(
                name: "tableStore",
                configureOptions: options => {
                    options.TableServiceClient = new TableServiceClient("UseDevelopmentStorage=true");
                }
            )
            .AddAzureBlobGrainStorage(
                name: "blobStore",
                configureOptions: options => {
                    options.BlobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                }
            )
            .AddAzureQueueStreams(
                name: "StreamProvider",
                configureOptions: builder => {
                    builder.Configure(options => {
                        options.QueueServiceClient = new QueueServiceClient("UseDevelopmentStorage=true");
                    });
                }
            )
            .AddAzureBlobGrainStorage(
                name: "PubSubStore",
                configureOptions: options => {
                    options.BlobServiceClient = new BlobServiceClient("UseDevelopmentStorage=true");
                }
            )
            .ConfigureLogging(logging => {
                logging
                    .AddFilter("Backend.Orleans", LogLevel.Debug) // Enable debug logging only for our loggers
                    .AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug)
                    .AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
                logging.AddConsole();
            })
            .Configure<GrainCollectionOptions>(options => {
                options.CollectionQuantum = TimeSpan.FromSeconds(10);
                options.CollectionAge = TimeSpan.FromSeconds(11);
            })
            .UseDashboard(options => { });
    })
    .ConfigureServices(services => {
        services
            .AddSingleton<IRealtimeUpdatesOrleans>(_ => {
                HubConnection connection = new HubConnectionBuilder()
                    .WithUrl("http://localhost:5202/realtimeUpdatesHubOrleans")
                    .WithAutomaticReconnect()
                    .Build();
                connection.StartAsync();
                return connection.ServerProxy<IRealtimeUpdatesOrleans>();
            })
            .AddSingleton<IPathfindingService, PathfindingService>();
    })
    .Build();
await host.RunAsync();