using Backend.Orleans.SharedContracts;
using Backend.SignalR.Classes;
using Backend.SignalR.SharedContracts;
using JetBrains.Annotations;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Backend.SignalR.Classes;

[TestSubject(typeof(RealtimeUpdatesHub<>))]
public class RealtimeUpdatesHubTest {
    [Fact]
    public async Task OnDisconnectedAsync_ShouldDeactivatePlayerGain() {
        // Arrange
        Exception? exception = null;
        string playerName = "Player one";

        Mock<HubCallerContext> contextMock = new();
        Mock<IClusterClient> clusterClientMock = new();
        Mock<IPlayerRegistry> playerRegistryMock = new();
        Mock<IPlayerGrain> playerGrainMock = new();
        Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>> loggerMock = new();
        Mock<RealtimeUpdatesHub<IRealtimeUpdatesClient>> hubMock = new(
            clusterClientMock.Object,
            loggerMock.Object);

        hubMock.CallBase = true;

        contextMock.Setup(x => x.Items)
            .Returns(new Dictionary<object, object?>() { { "PlayerName", playerName } })
            .Verifiable(
                Times.Exactly(
                    1)); // Called twice: once when test is initializing, once when OnDisconnectedAsync is called

        hubMock.Object.Context = contextMock.Object;

        clusterClientMock.Setup(x => x.GetGrain<IPlayerRegistry>(Guid.Empty, null))
            .Returns(playerRegistryMock.Object)
            .Verifiable(Times.Once);
        playerRegistryMock.Setup(x => x.FindPlayerByName(playerName))
            .Returns(Task.FromResult<IPlayerGrain?>(playerGrainMock.Object))
            .Verifiable(Times.Once);
        playerGrainMock.Setup(x => x.DeactivateOnIdle())
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Act
        // await hub.OnDisconnectedAsync(exception);
        await hubMock.Object.OnDisconnectedAsync(exception);

        // Assert
        contextMock.Verify();
        clusterClientMock.Verify();
        playerRegistryMock.Verify();
        playerGrainMock.Verify();
    }

    [Fact]
    public async Task OnDisconnectedAsync_ShouldThrowExceptionWhenPlayerIsNotFound() {
        // Arrange
        Mock<HubCallerContext> contextMock = new();
        Mock<IClusterClient> clusterClientMock = new();
        Mock<ILogger<RealtimeUpdatesHub<IRealtimeUpdatesClient>>> loggerMock = new();
        Mock<RealtimeUpdatesHub<IRealtimeUpdatesClient>> hubMock = new(
            clusterClientMock.Object,
            loggerMock.Object);

        hubMock.CallBase = true;

        contextMock.Setup(x => x.Items)
            .Returns(new Dictionary<object, object?>() { { "PlayerName", null } })
            .Verifiable(
                Times.Exactly(
                    1));

        hubMock.Object.Context = contextMock.Object;

        // Act
        var act = () => hubMock.Object.OnDisconnectedAsync(null);

        // Assert
        Exception exception = await Assert.ThrowsAsync<Exception>(act);
        Assert.Equal("Player name not found in context items", exception.Message);
    }
}