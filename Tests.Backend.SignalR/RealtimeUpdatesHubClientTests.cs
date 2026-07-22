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
    private readonly Mock<IClusterClient> _orleansClientMock;
    private readonly Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>> _loggerMock;
    private readonly RealtimeUpdatesHubClient _realtimeUpdatesHubClient;
    private const string PlayerName = "Test Player";
    private const string ConnectionId = "Connection123";

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
    public async Task GetPlayersInCurrentChunk_ShouldReturnPlayersInChunk_WhenPlayerFound() {
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
}