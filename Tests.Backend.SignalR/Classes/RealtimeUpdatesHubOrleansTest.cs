using Backend.SignalR.Classes;
using Backend.SignalR.SharedContracts;
using Castle.Core.Logging;
using JetBrains.Annotations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Backend.SignalR.Classes;

[TestSubject(typeof(RealtimeUpdatesHubOrleans))]
public class RealtimeUpdatesHubOrleansTest {
    public RealtimeUpdatesHubOrleansTest() {
        Mock<IClusterClient> clusterClientMock = new();
        Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>> loggerMock = new();
        _realtimeUpdatesHubClientContextMock =
            new Mock<IHubContext<RealtimeUpdatesHubClient, IRealtimeUpdatesClient>>();
        _realtimeUpdatesClientMock = new Mock<IRealtimeUpdatesClient>();

        _hub = new RealtimeUpdatesHubOrleans(
            clusterClientMock.Object,
            loggerMock.Object,
            _realtimeUpdatesHubClientContextMock.Object);
    }

    private readonly Mock<IHubContext<RealtimeUpdatesHubClient, IRealtimeUpdatesClient>>
        _realtimeUpdatesHubClientContextMock;
    private readonly Mock<IRealtimeUpdatesClient> _realtimeUpdatesClientMock;
    private readonly RealtimeUpdatesHubOrleans _hub;

    [Fact]
    public async Task AddToGroupAsync_ShouldBroadcast() {
        // Arrange
        string groupName = "Test Group";
        string connectionId = "Test Connection";

        _realtimeUpdatesHubClientContextMock.Setup(x => x.Groups.AddToGroupAsync(
                connectionId,
                groupName,
                It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        // Act
        await _hub.AddToGroupAsync(groupName, connectionId);

        // Assert
        _realtimeUpdatesHubClientContextMock.Verify();
    }

    [Fact]
    public async Task PlayerAddedToChunk_ShouldBroadcast() {
        // Arrange
        string groupName = "Test Group";
        string playerId = "Test Player";
        int[][] path = new[] {
            new[] { 1, 2 },
            new[] { 3, 4 },
            new[] { 5, 6 }
        };
        string playerName = "Test Player";
        long chunkId = 1L;
        int posX = 0;
        int posY = 1;

        _realtimeUpdatesHubClientContextMock.Setup(x => x.Clients.Group(groupName))
            .Returns(_realtimeUpdatesClientMock.Object)
            .Verifiable(Times.Once);
        _realtimeUpdatesClientMock.Setup(x => x.PlayerAddedToChunk(playerId, playerName, chunkId, posX, posY, path))
            .Verifiable(Times.Once);

        // Act
        await _hub.PlayerAddedToChunk(groupName, playerId, playerName, chunkId, posX, posY, path);

        // Assert
        _realtimeUpdatesClientMock.Verify();
    }

    [Fact]
    public async Task PlayerNewPathCreated_ShouldBroadcast() {
        // Arrange
        string groupName = "Test Group";
        string playerId = "Test Player";
        int[][] path = new[] {
            new[] { 1, 2 },
            new[] { 3, 4 },
            new[] { 5, 6 }
        };

        _realtimeUpdatesHubClientContextMock.Setup(x => x.Clients.Group(groupName))
            .Returns(_realtimeUpdatesClientMock.Object)
            .Verifiable(Times.Once);
        _realtimeUpdatesClientMock.Setup(x => x.PlayerNewPathCreated(playerId, path))
            .Verifiable(Times.Once);

        // Act
        await _hub.PlayerNewPathCreated(groupName, playerId, path);

        // Assert
        _realtimeUpdatesClientMock.Verify();
    }

    [Fact]
    public async Task PlayerRemovedFromChunk_ShouldBroadcast() {
        // Arrange
        string groupName = "Test Group";
        string playerId = "Test Player";
        long chunkId = 1L;

        _realtimeUpdatesHubClientContextMock.Setup(x => x.Clients.Group(groupName))
            .Returns(_realtimeUpdatesClientMock.Object)
            .Verifiable(Times.Once);
        _realtimeUpdatesClientMock.Setup(x => x.PlayerRemovedFromChunk(playerId, chunkId))
            .Verifiable(Times.Once);

        // Act
        await _hub.PlayerRemovedFromChunk(groupName, playerId, chunkId);

        // Assert
        _realtimeUpdatesClientMock.Verify();
    }

    [Fact]
    public async Task RemoveFromGroupAsync_ShouldBroadcast() {
        // Arrange
        string groupName = "Test Group";
        string connectionId = "Test Connection";

        _realtimeUpdatesHubClientContextMock.Setup(x => x.Groups.RemoveFromGroupAsync(
                connectionId,
                groupName,
                It.IsAny<CancellationToken>()))
            .Verifiable(Times.Once);

        // Act
        await _hub.RemoveFromGroupAsync(groupName, connectionId);

        // Assert
        _realtimeUpdatesHubClientContextMock.Verify();
    }
}