using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using JetBrains.Annotations;
using Moq;
using Orleans.TestKit;

namespace Tests.Backend.Orleans.GrainClasses;

[TestSubject(typeof(WorldChunkGrain))]
public class WorldChunkGrainTest : TestKitBase {
    [Fact]
    public async Task AddPlayer_ShouldNotifyRealtimeUpdatesAndAvoidDuplicates() {
        // Arrange
        long chunkId = 5L;
        string groupName = chunkId.ToString();
        string playerKey = Guid.NewGuid().ToString();
        string playerName = "Player One";
        SerializableVector2 position = new SerializableVector2(1, 2);

        // Mock realtime updates service
        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock
            .Setup(x => x.PlayerAddedToChunk(groupName, playerKey, playerName, position.X, position.Y))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Create grain
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        await grain.AddPlayer(playerKey, playerName, position);
        // Duplicate add should be ignored
        await grain.AddPlayer(playerKey, playerName, position);

        // Assert
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task RemovePlayer_ShouldNotifyWhenPresentAndIgnoreWhenAbsent() {
        // Arrange
        long chunkId = 7L;
        string groupName = chunkId.ToString();
        string playerKey = Guid.NewGuid().ToString();
        string playerName = "Player Two";
        SerializableVector2 position = new SerializableVector2(3, 4);

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock
            .Setup(x => x.PlayerRemovedFromChunk(groupName, playerKey))
            .Returns(Task.CompletedTask);

        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act & Assert
        // Removing an absent player should do nothing
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Add then remove -> should notify once
        await grain.AddPlayer(playerKey, playerName, position);
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(groupName, playerKey), Times.Once);

        // Removing again should still be ignored
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(groupName, playerKey), Times.Once);
    }

    [Fact]
    public async Task GetRealtimeUpdatesGroupName_ShouldReturnPrimaryKeyString() {
        // Arrange
        long chunkId = 42L;
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        string group = await grain.GetRealtimeUpdatesGroupName();

        // Assert
        Assert.Equal(chunkId.ToString(), group);
    }

    [Fact]
    public async Task GetAllPlayers_ShouldReturnGrainRefsForStoredPlayerKeys() {
        // Arrange
        long chunkId = 9L;
        var realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        // No need to verify add notifications here
        realtimeUpdatesMock
            .Setup(x => x.PlayerAddedToChunk(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Mock cluster client to return player grain references
        Mock<IClusterClient> clusterClientMock = Silo.AddServiceProbe<IClusterClient>();

        Guid p1 = Guid.NewGuid();
        Guid p2 = Guid.NewGuid();
        string k1 = p1.ToString();
        string k2 = p2.ToString();

        var player1Mock = new Mock<IPlayerGrain>();
        var player2Mock = new Mock<IPlayerGrain>();

        clusterClientMock.Setup(x => x.GetGrain<IPlayerGrain>(p1, null))
            .Returns(player1Mock.Object)
            .Verifiable(Times.Once);
        clusterClientMock.Setup(x => x.GetGrain<IPlayerGrain>(p2, null))
            .Returns(player2Mock.Object)
            .Verifiable(Times.Once);

        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Add two players
        await grain.AddPlayer(k1, "Alice", new SerializableVector2(10, 20));
        await grain.AddPlayer(k2, "Bob", new SerializableVector2(30, 40));

        // Act
        List<IPlayerGrain> players = await grain.GetAllPlayers();

        // Assert
        Assert.Equal(2, players.Count);
        Assert.Contains(player1Mock.Object, players);
        Assert.Contains(player2Mock.Object, players);
        clusterClientMock.Verify();
    }

    [Fact]
    public void SizeConstants_ShouldBe30() {
        Assert.Equal(30, WorldChunkGrain.SizeX);
        Assert.Equal(30, WorldChunkGrain.SizeY);
    }
}