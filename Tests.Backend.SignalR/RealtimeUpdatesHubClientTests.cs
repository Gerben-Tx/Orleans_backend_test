using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.Classes;
using Backend.SignalR.SharedContracts;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Orleans.TestKit;

namespace Tests.Backend.SignalR;

public class RealtimeUpdatesHubClientTests : TestKitBase {
    public RealtimeUpdatesHubClientTests() {
        _orleansClientMock = new Mock<IClusterClient>();
        _loggerMock = new Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>>();

        _realtimeUpdatesHubClient = new RealtimeUpdatesHubClient(_orleansClientMock.Object, _loggerMock.Object);

        // Context mock
        var contextMock = new Mock<HubCallerContext>();
        _realtimeUpdatesHubClient.Context = contextMock.Object;
        contextMock.Setup(x => x.ConnectionId)
            .Returns(ConnectionId);
        contextMock.Setup(x => x.Items)
            .Returns(
                new Dictionary<object, object?> {
                    { "PlayerName", PlayerName }
                });
    }

    private readonly Mock<IClusterClient> _orleansClientMock;
    private readonly Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>> _loggerMock;
    private readonly RealtimeUpdatesHubClient _realtimeUpdatesHubClient;
    private const string PlayerName = "Test Player";
    private const string ConnectionId = "Connection123";

    [Fact]
    public async Task DebugMoveToChunk_ShouldEnter_WhenPlayerFound() {
        // Arrange
        int newChunkId = 2;

        // Chunk grain mock
        var newChunkGrainMock = new Mock<IWorldChunkGrain>();
        _orleansClientMock.Setup(x => x.GetGrain<IWorldChunkGrain>(newChunkId, null))
            .Returns(newChunkGrainMock.Object)
            .Verifiable(Times.Once);

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        playerGrainMock.Setup(x => x.DebugMoveToChunk(newChunkGrainMock.Object))
            .Verifiable(Times.Once);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object)
            .Verifiable(Times.Once);

        // Act
        await _realtimeUpdatesHubClient.DebugMoveToChunk(PlayerName, newChunkId);

        // Assert
        _orleansClientMock.Verify();
        newChunkGrainMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
    }

    [Fact]
    public async Task GetCurrentChunkId_ShouldReturnChunkId_WhenPlayerFound() {
        // Arrange

        // Chunk grain mock
        var currentChunkPrimaryKey = 5L;
        var currentChunkMock = new Mock<IWorldChunkGrain>();
        currentChunkMock.Setup(x => x.GetKey())
            .Returns(Task.FromResult(currentChunkPrimaryKey))
            .Verifiable(Times.Once);
        currentChunkMock.Setup(x => x.GetPosition())
            .Returns(Task.FromResult<WorldChunkGrainPosition?>(new WorldChunkGrainPosition(0, 0)))
            .Verifiable(Times.Once);

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        _orleansClientMock.Verify(
            x => x.GetGrain<IPlayerGrain>(It.IsAny<Guid>(), null),
            Times.Never);
        playerGrainMock.Setup(x => x.Initialize(ConnectionId, PlayerName))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        playerGrainMock.Setup(x => x.GetCurrentChunk())
            .Returns(Task.FromResult(currentChunkMock.Object))
            .Verifiable(Times.Once);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object)
            .Verifiable(Times.Once);

        // Act
        var result = await _realtimeUpdatesHubClient.GetCurrentChunk(PlayerName);

        // Assert
        Assert.Equal(currentChunkPrimaryKey, result.ChunkId);
        currentChunkMock.Verify();
        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
    }

    [Fact]
    public async Task GetCurrentChunkId_ShouldReturnNull_WhenPlayerMissing() {
        // Arrange

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync((IPlayerGrain?)null)
            .Verifiable(Times.Once);

        // Act
        var result = await _realtimeUpdatesHubClient.GetCurrentChunk(PlayerName);

        // Assert
        Assert.Null(result);
        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
    }

    [Fact]
    public async Task GetPlayersInCurrentChunk_ShouldReturnEmptyList_WhenPlayerMissing() {
        // Arrange

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync((IPlayerGrain?)null)
            .Verifiable(Times.Once);

        // Act
        var result = await _realtimeUpdatesHubClient.GetPlayersInChunk(PlayerName, 1L);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task
        GetPlayersInCurrentChunk_ShouldReturnPlayersInCurrentChunk_WhenPlayerFoundAndChunkIdIsCurrentChunkId() {
        // Arrange
        var chunkId = 1L;
        var player1 = new Mock<IPlayerGrain>();
        var player2 = new Mock<IPlayerGrain>();
        var player1Position = new SerializableVector2(100, 200);
        var player2Position = new SerializableVector2(105, 205);
        var player1Name = "Player 1";
        var player2Name = "Player 2";
        var player1Key = "Player 1 key";
        var player2Key = "Player 2 key";
        var playersInChunk = new List<IPlayerGrain>();
        playersInChunk.Add(player1.Object);
        playersInChunk.Add(player2.Object);
        var expectedMessages = new List<PlayerListMessage> {
            new() {
                Id = player1Key,
                Name = player1Name,
                PositionX = player1Position.X,
                PositionY = player1Position.Y
            },
            new() {
                Id = player2Key,
                Name = player2Name,
                PositionX = player2Position.X,
                PositionY = player2Position.Y
            }
        };

        // Chunk grain mock
        var currentChunkGrainMock = new Mock<IWorldChunkGrain>();
        _orleansClientMock.Setup(x => x.GetGrain<IWorldChunkGrain>(It.IsAny<long>(), null))
            .Returns(currentChunkGrainMock.Object)
            .Verifiable(Times.Once);
        currentChunkGrainMock.Setup(x => x.GetAllPlayers())
            .Returns(Task.FromResult(playersInChunk))
            .Verifiable(Times.Once);
        currentChunkGrainMock.Setup(x => x.GetKey())
            .Returns(Task.FromResult(chunkId))
            .Verifiable(Times.Once);

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        playerGrainMock.Setup(x => x.GetCurrentChunk())
            .Returns(Task.FromResult(currentChunkGrainMock.Object))
            .Verifiable(Times.Once);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object)
            .Verifiable(Times.Once);

        // Players in chunk mock
        player1.Setup(x => x.GetPosition())
            .Returns(Task.FromResult(player1Position))
            .Verifiable(Times.Once);
        player1.Setup(x => x.GetName())
            .Returns(Task.FromResult(player1Name))
            .Verifiable(Times.Once);
        player1.Setup(x => x.GetKey())
            .Returns(Task.FromResult(player1Key))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetPosition())
            .Returns(Task.FromResult(player2Position))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetName())
            .Returns(Task.FromResult(player2Name))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetKey())
            .Returns(Task.FromResult(player2Key))
            .Verifiable(Times.Once);

        // Act
        var result = await _realtimeUpdatesHubClient.GetPlayersInChunk(PlayerName, chunkId);

        // Assert
        Assert.Collection(
            expectedMessages,
            x => Assert.Equivalent(x, result[0]),
            x => Assert.Equivalent(x, result[1])
        );
    }

    [Fact]
    public async Task
        GetPlayersInCurrentChunk_ShouldReturnPlayersInMatchingVisibleChunk_WhenPlayerFoundAndChunkIdIsNotCurrentChunkId() {
        // Arrange
        var chunkId = 1L;
        var player1 = new Mock<IPlayerGrain>();
        var player2 = new Mock<IPlayerGrain>();
        var player1Position = new SerializableVector2(100, 200);
        var player2Position = new SerializableVector2(105, 205);
        var player1Name = "Player 1";
        var player2Name = "Player 2";
        var player1Key = "Player 1 key";
        var player2Key = "Player 2 key";
        var playersInChunk = new List<IPlayerGrain>();
        playersInChunk.Add(player1.Object);
        playersInChunk.Add(player2.Object);
        var expectedMessages = new List<PlayerListMessage> {
            new() {
                Id = player1Key,
                Name = player1Name,
                PositionX = player1Position.X,
                PositionY = player1Position.Y
            },
            new() {
                Id = player2Key,
                Name = player2Name,
                PositionX = player2Position.X,
                PositionY = player2Position.Y
            }
        };
        int radius = 10;
        VisibleWorldChunk[] visibleWorldChunks = new[] {
            new VisibleWorldChunk(chunkId - 1, new WorldChunkGrainPosition(0, 0)),
            new VisibleWorldChunk(chunkId, new WorldChunkGrainPosition(0, 1)),
            new VisibleWorldChunk(chunkId + 1, new WorldChunkGrainPosition(0, 2)),
            new VisibleWorldChunk(chunkId + 2, new WorldChunkGrainPosition(0, 3)),
        };
        Mock<IWorldChunkGrain> visibleWorldChunkMock = new();

        // Chunk grain mock
        var currentChunkGrainMock = new Mock<IWorldChunkGrain>();
        _orleansClientMock.Setup(x => x.GetGrain<IWorldChunkGrain>(It.IsAny<long>(), null))
            .Returns(currentChunkGrainMock.Object)
            .Verifiable(Times.Once);
        currentChunkGrainMock.Setup(x => x.GetAllPlayers())
            .Returns(Task.FromResult(playersInChunk))
            .Verifiable(Times.Once);
        currentChunkGrainMock.Setup(x => x.GetKey())
            .Returns(Task.FromResult(0L))
            .Verifiable(Times.Once);
        currentChunkGrainMock.Setup(x => x.GetVisibleChunks(radius))
            .Returns(Task.FromResult(visibleWorldChunks))
            .Verifiable(Times.Once);

        // Visible world chunk grain mock
        VisibleWorldChunk visibleWorldChunk = visibleWorldChunks.ToArray().First(chunk => chunk.Id == chunkId);
        _orleansClientMock.Setup(x => x.GetGrain<IWorldChunkGrain>(visibleWorldChunk.Id, null))
            .Returns(visibleWorldChunkMock.Object)
            .Verifiable(Times.Once);
        visibleWorldChunkMock.Setup(x => x.GetAllPlayers())
            .Returns(Task.FromResult(playersInChunk))
            .Verifiable(Times.Once);

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        playerGrainMock.Setup(x => x.GetCurrentChunk())
            .Returns(Task.FromResult(currentChunkGrainMock.Object))
            .Verifiable(Times.Once);
        playerGrainMock.Setup(x => x.GetChunkVisibilityRadius())
            .Returns(Task.FromResult(radius))
            .Verifiable(Times.Once);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object)
            .Verifiable(Times.Once);

        // Players in chunk mock
        player1.Setup(x => x.GetPosition())
            .Returns(Task.FromResult(player1Position))
            .Verifiable(Times.Once);
        player1.Setup(x => x.GetName())
            .Returns(Task.FromResult(player1Name))
            .Verifiable(Times.Once);
        player1.Setup(x => x.GetKey())
            .Returns(Task.FromResult(player1Key))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetPosition())
            .Returns(Task.FromResult(player2Position))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetName())
            .Returns(Task.FromResult(player2Name))
            .Verifiable(Times.Once);
        player2.Setup(x => x.GetKey())
            .Returns(Task.FromResult(player2Key))
            .Verifiable(Times.Once);

        // Act
        var result = await _realtimeUpdatesHubClient.GetPlayersInChunk(PlayerName, chunkId);

        // Assert
        Assert.Collection(
            expectedMessages,
            x => Assert.Equivalent(x, result[0]),
            x => Assert.Equivalent(x, result[1])
        );
    }

    [Fact]
    public async Task GetVisibleChunks_ShouldReturnEmptyVisibleWorldChunksMessageIfPlayerNotFound() {
        // Arrange
        int radius = 10;
        Mock<IPlayerRegistry> playerRegistryMock = new();
        Mock<IPlayerGrain> playerGrainMock = new();
        Mock<IWorldChunkGrain> chunkGrainMock = new();

        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .Returns(Task.FromResult<IPlayerGrain?>(null));

        // Act
        VisibleWorldChunksMessage message = await _realtimeUpdatesHubClient.GetVisibleChunks(PlayerName, radius);

        // Assert
        Assert.Empty(message.Chunks);

        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
        chunkGrainMock.Verify();
    }

    [Fact]
    public async Task GetVisibleChunks_ShouldReturnVisibleWorldChunksMessage() {
        // Arrange
        int radius = 10;
        VisibleWorldChunk[] expectedWorldChunks = new[] {
            new VisibleWorldChunk(0L, new WorldChunkGrainPosition(0, 0)),
            new VisibleWorldChunk(1L, new WorldChunkGrainPosition(0, 1)),
            new VisibleWorldChunk(2L, new WorldChunkGrainPosition(0, 2)),
            new VisibleWorldChunk(3L, new WorldChunkGrainPosition(0, 3)),
        };
        Mock<IPlayerRegistry> playerRegistryMock = new();
        Mock<IPlayerGrain> playerGrainMock = new();
        Mock<IWorldChunkGrain> chunkGrainMock = new();

        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .Returns(Task.FromResult<IPlayerGrain?>(playerGrainMock.Object));
        playerGrainMock.Setup(x => x.GetCurrentChunk())
            .Returns(Task.FromResult(chunkGrainMock.Object));
        chunkGrainMock.Setup(x => x.GetVisibleChunks(radius))
            .Returns(Task.FromResult(expectedWorldChunks));

        // Act
        VisibleWorldChunksMessage message = await _realtimeUpdatesHubClient.GetVisibleChunks(PlayerName, radius);

        // Assert
        for (int i = 0; i < message.Chunks.Length; i++) {
            Assert.IsType<WorldChunkContract>(message.Chunks[i]);
            Assert.Equal(expectedWorldChunks[i].Id, message.Chunks[i].ChunkId);
            Assert.Equal(expectedWorldChunks[i].Position.X, message.Chunks[i].X);
            Assert.Equal(expectedWorldChunks[i].Position.Y, message.Chunks[i].Y);
        }

        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
        chunkGrainMock.Verify();
    }

    [Fact]
    public async Task GetWorldInfo_ShouldReturnWorldInfoMessage() {
        // Arrange
        ulong ticks = 1000;
        uint ticksPerSecond = 100;

        Mock<ITickGrain> tickGrainMock = new();
        _orleansClientMock.Setup(x => x.GetGrain<ITickGrain>(ITickGrain.Key, null))
            .Returns(tickGrainMock.Object);
        tickGrainMock.Setup(x => x.GetTicks())
            .Returns(Task.FromResult(ticks));
        tickGrainMock.Setup(x => x.GetTicksPerSecond())
            .Returns(Task.FromResult(ticksPerSecond));

        // Act
        WorldInfoMessage message = await _realtimeUpdatesHubClient.GetWorldInfo();

        // Assert
        Assert.Equal(IWorldChunkGrain.WorldSizeX, message.WorldSizeX);
        Assert.Equal(IWorldChunkGrain.WorldSizeY, message.WorldSizeY);
        Assert.Equal(IWorldChunkGrain.SizeX, message.ChunkSizeX);
        Assert.Equal(IWorldChunkGrain.SizeY, message.ChunkSizeY);
        Assert.Equal(ticks, message.CurrentTick);
        Assert.Equal(ticksPerSecond, message.TicksPerSecond);
    }

    [Fact]
    public async Task MoveToChunk_ShouldNoOp_WhenPlayerMissing() {
        // Arrange
        int newChunkId = 2;

        // Player grain mock
        _orleansClientMock.Verify(
            x => x.GetGrain<IPlayerGrain>(It.IsAny<Guid>(), null),
            Times.Never);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync((IPlayerGrain?)null)
            .Verifiable(Times.Once);

        // Act
        await _realtimeUpdatesHubClient.DebugMoveToChunk(PlayerName, newChunkId);

        // Assert
        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
    }

    [Fact]
    public async Task RegisterPlayerGrain_ShouldCreateNew_WhenNotFound() {
        // Arrange

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Exactly(2));
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync((IPlayerGrain?)null)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.AddPlayer(PlayerName, It.IsAny<Guid>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerGrain>(It.IsAny<Guid>(), null))
            .Returns(playerGrainMock.Object)
            .Verifiable(Times.Once);
        playerGrainMock.Setup(x => x.Initialize(ConnectionId, PlayerName))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        await _realtimeUpdatesHubClient.RegisterPlayerGrain(PlayerName);

        // Assert
        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
    }

    [Fact]
    public async Task RegisterPlayerGrain_ShouldUseExisting_WhenFound() {
        // Arrange

        // Player grain mock
        var playerGrainMock = new Mock<IPlayerGrain>();
        _orleansClientMock.Verify(
            x => x.GetGrain<IPlayerGrain>(It.IsAny<Guid>(), null),
            Times.Never);
        playerGrainMock.Setup(x => x.Initialize(ConnectionId, PlayerName))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Player registry mock
        var playerRegistryMock = new Mock<IPlayerRegistry>();
        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Verify(
            x => x.AddPlayer(It.IsAny<string>(), It.IsAny<Guid>()),
            Times.Never);

        // Act
        await _realtimeUpdatesHubClient.RegisterPlayerGrain(PlayerName);

        // Assert
        _orleansClientMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
    }

    [Fact]
    public async Task SendMovementIntent_ShouldCallPlayerGrain() {
        // Arrange
        SerializableVector2 destination = new(10, 20);
        ulong tick = 1000;

        Mock<IPlayerRegistry> playerRegistryMock = new();
        Mock<IPlayerGrain> playerGrainMock = new();

        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .ReturnsAsync(playerGrainMock.Object);
        playerGrainMock.Setup(x => x.ReceiveMovementIntent(destination.X, destination.Y, tick))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        await _realtimeUpdatesHubClient.SendMovementIntent(PlayerName, destination.X, destination.Y, tick);

        // Assert
        playerGrainMock.Verify();
    }

    [Fact]
    public async Task SendMovementIntent_ShouldNotCallPlayerGrainIfPlayerIsNotFound() {
        // Arrange
        SerializableVector2 destination = new(10, 20);
        ulong tick = 1000;

        Mock<IPlayerRegistry> playerRegistryMock = new();
        Mock<IPlayerGrain> playerGrainMock = new();

        _orleansClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object);
        playerRegistryMock.Setup(x => x.FindPlayerByName(PlayerName))
            .Returns(Task.FromResult<IPlayerGrain?>(null));
        playerGrainMock.Setup(x => x.ReceiveMovementIntent(destination.X, destination.Y, tick))
            .Verifiable(Times.Never);

        // Act
        await _realtimeUpdatesHubClient.SendMovementIntent(PlayerName, destination.X, destination.Y, tick);

        // Assert
        playerGrainMock.Verify();
    }
}