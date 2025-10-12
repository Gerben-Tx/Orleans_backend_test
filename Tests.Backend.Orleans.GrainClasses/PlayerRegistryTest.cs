using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using JetBrains.Annotations;
using Moq;
using Microsoft.Extensions.Logging;

namespace Tests.Backend.Orleans.GrainClasses;

[TestSubject(typeof(PlayerRegistry))]
public class PlayerRegistryTest {
    private static PlayerRegistry CreateSut(
        PlayerRegistryState? initialState,
        out Mock<IPersistentState<PlayerRegistryState>> stateMock,
        out Mock<IClusterClient> clusterClientMock,
        out Mock<IPlayerGrain> playerGrainMock
    ) {
        // Persistent state with a real dictionary so we can assert mutations
        PlayerRegistryState state = initialState ?? new PlayerRegistryState();
        stateMock = new Mock<IPersistentState<PlayerRegistryState>>();
        stateMock.SetupGet(s => s.State).Returns(state);
        stateMock.Setup(s => s.WriteStateAsync()).Returns(Task.CompletedTask);

        // Cluster client returns a mock grain for any guid
        clusterClientMock = new Mock<IClusterClient>();
        playerGrainMock = new Mock<IPlayerGrain>();
        clusterClientMock
            .Setup(c => c.GetGrain<IPlayerGrain>(It.IsAny<Guid>(), null))
            .Returns(playerGrainMock.Object);

        // Logger is not asserted here; pass a no-op mock
        Mock<ILogger<PlayerRegistry>> loggerMock = new();

        return new PlayerRegistry(stateMock.Object, loggerMock.Object, clusterClientMock.Object);
    }

    [Fact]
    public async Task AddPlayer_ShouldAdd_And_Persist() {
        // Arrange
        PlayerRegistry sut = CreateSut(null, out var stateMock, out var clusterClientMock, out var playerGrainMock);
        string name = "Alice";
        Guid guid = Guid.NewGuid();

        // Act
        await sut.AddPlayer(name, guid);

        // Assert
        Assert.True(stateMock.Object.State.Players.TryGetValue(name, out var storedGrain));
        Assert.Same(playerGrainMock.Object, storedGrain);
        stateMock.Verify(s => s.WriteStateAsync(), Times.Once);
        clusterClientMock.Verify(c => c.GetGrain<IPlayerGrain>(guid, null), Times.Once);
    }

    [Fact]
    public async Task RemovePlayer_ShouldRemove_And_Persist() {
        // Arrange: pre-seed with an entry
        var existingGrain = new Mock<IPlayerGrain>().Object;
        var seededState = new PlayerRegistryState();
        seededState.Players["Bob"] = existingGrain;
        PlayerRegistry sut = CreateSut(seededState, out var stateMock, out _, out _);

        // Act
        await sut.RemovePlayer("Bob");

        // Assert
        Assert.False(stateMock.Object.State.Players.ContainsKey("Bob"));
        stateMock.Verify(s => s.WriteStateAsync(), Times.Once);
    }

    [Fact]
    public async Task FindPlayerByName_ShouldReturnExisting() {
        // Arrange
        var existingGrainMock = new Mock<IPlayerGrain>();
        var seededState = new PlayerRegistryState();
        seededState.Players["Charlie"] = existingGrainMock.Object;
        PlayerRegistry sut = CreateSut(seededState, out _, out _, out _);

        // Act
        IPlayerGrain? result = await sut.FindPlayerByName("Charlie");

        // Assert
        Assert.NotNull(result);
        Assert.Same(existingGrainMock.Object, result);
    }

    [Fact]
    public async Task FindPlayerByName_ShouldReturnNull_WhenMissing() {
        // Arrange
        PlayerRegistry sut = CreateSut(null, out _, out _, out _);

        // Act
        IPlayerGrain? result = await sut.FindPlayerByName("DoesNotExist");

        // Assert
        Assert.Null(result);
    }
}