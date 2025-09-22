using Backend.Orleans.SharedContracts;
using Microsoft.AspNetCore.SignalR;
using Orleans.Streams;

namespace Backend.SignalR.Classes;

public class OrleansStreamListenerService : IHostedService {
    private readonly IClusterClient _orleansClient;
    private readonly IHubContext<RealtimeUpdatesHub> _hubContext;
    private readonly ILogger<OrleansStreamListenerService> _logger;

    public OrleansStreamListenerService(
        IClusterClient orleansClient,
        IHubContext<RealtimeUpdatesHub> hubContext,
        ILogger<OrleansStreamListenerService> logger
    ) {
        _orleansClient = orleansClient;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        await CreateWorldChunkStream();
        await CreatePlayerStream();

        _logger.LogDebug("Stream service started");
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }

    private async Task CreateWorldChunkStream() {
        IAsyncStream<IStreamMessage> stream = _orleansClient
            .GetStreamProvider("StreamProvider")
            .GetStream<IStreamMessage>("MAIN_STREAM", "WORLD_CHUNK");

        await stream.SubscribeAsync(async (message, token) => {
            _logger.LogDebug("Received message on stream '{Stream}': {Message}",
                ($"{stream.StreamId.GetNamespace()}/{stream.StreamId.GetKeyAsString()}"),
                Newtonsoft.Json.JsonConvert.SerializeObject(message)
            );
            
            await _hubContext.Clients.Group(message.GroupName).SendAsync(message.Method, message.Data);
        });
    }
    
    private async Task CreatePlayerStream() {
        IAsyncStream<IStreamMessage> stream = _orleansClient
            .GetStreamProvider("StreamProvider")
            .GetStream<IStreamMessage>("MAIN_STREAM", "PLAYER");

        await stream.SubscribeAsync(async (message, token) => {
            _logger.LogDebug("Received message on stream '{Stream}': {Message}",
                ($"{stream.StreamId.GetNamespace()}/{stream.StreamId.GetKeyAsString()}"),
                Newtonsoft.Json.JsonConvert.SerializeObject(message)
            );

            // TODO: i do not like this way of doing it...
            switch (message.Method) {
                case "AddToGroupAsync":
                    await _hubContext.Groups.AddToGroupAsync((string)message.Data, message.GroupName);
                    break;
                case "RemoveFromGroupAsync":
                    await _hubContext.Groups.RemoveFromGroupAsync((string)message.Data, message.GroupName);
                    break;
                default:
                    await _hubContext.Clients.Group(message.GroupName).SendAsync(message.Method, message.Data);
                    break;
            }
        });
    }
}