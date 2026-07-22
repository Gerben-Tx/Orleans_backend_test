using System.Diagnostics.CodeAnalysis;
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
            .Setup(x => x.PlayerAddedToChunk(groupName, playerKey, playerName, chunkId, position.X, position.Y))
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
            .Setup(x => x.PlayerRemovedFromChunk(groupName, playerKey, chunkId))
            .Returns(Task.CompletedTask);

        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act & Assert
        // Removing an absent player should do nothing
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(It.IsAny<string>(), It.IsAny<string>(), chunkId), Times.Never);

        // Add then remove -> should notify once
        await grain.AddPlayer(playerKey, playerName, position);
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(groupName, playerKey, chunkId), Times.Once);

        // Removing again should still be ignored
        await grain.RemovePlayer(playerKey, playerName);
        realtimeUpdatesMock.Verify(x => x.PlayerRemovedFromChunk(groupName, playerKey, chunkId), Times.Once);
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
                chunkId,
                It.IsAny<int>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        Guid p1 = Guid.NewGuid();
        Guid p2 = Guid.NewGuid();
        string k1 = p1.ToString();
        string k2 = p2.ToString();

        // Mock player grains
        Mock<IPlayerGrain> player1Mock = Silo.AddProbe<IPlayerGrain>(p1);
        Mock<IPlayerGrain> player2Mock = Silo.AddProbe<IPlayerGrain>(p2);

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
    }

    [Fact]
    public void SizeConstants_ShouldBe30() {
        Assert.Equal(30, WorldChunkGrain.SizeX);
        Assert.Equal(30, WorldChunkGrain.SizeY);
    }

    [Theory]
    [InlineData(0, 0, 0)] // Top left corner
    [InlineData(WorldChunkGrain.WorldSizeX, 0, 1)] // First one, second row
    [InlineData(WorldChunkGrain.WorldSizeX - 1, 9, 0)] // Top right corner
    [InlineData(
        (WorldChunkGrain.WorldSizeX - 1) * WorldChunkGrain.WorldSizeY,
        0,
        WorldChunkGrain.WorldSizeY - 1)] // Bottom left corner
    [InlineData(
        WorldChunkGrain.WorldSizeX * WorldChunkGrain.WorldSizeY - 1,
        WorldChunkGrain.WorldSizeX - 1,
        WorldChunkGrain.WorldSizeY - 1)] // Bottom right corner
    public async Task GetPositionByChunkId_ShouldReturnCorrectPosition(
        long chunkId,
        int expectedX,
        int expectedY
    ) {
        // Arrange
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);
        
        // Act
        WorldChunkGrainPosition? position = await grain.GetPositionByChunkId(chunkId);

        // Assert
        Assert.Equal($"{expectedX},{expectedY}", $"{position?.X},{position?.Y}");
    }

    [Fact]
    [SuppressMessage("ReSharper", "UselessBinaryOperation")]
    public async Task GetVisibleChunks_ShouldReturnVisibleWorldChunksWhenChunkIsTopLeft() {
        // Arrange
        long chunkId = 0;
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        VisibleWorldChunk[] visibleChunks = await grain.GetVisibleChunksById();

        // Assert
        Assert.Equal(4, visibleChunks.Length);
        Assert.Equal(chunkId + 1, visibleChunks[0].Id); // East
        Assert.Equal(chunkId, visibleChunks[1].Id); // Center
        Assert.Equal(chunkId + 1 + WorldChunkGrain.WorldSizeX, visibleChunks[2].Id); // SouthEast
        Assert.Equal(chunkId + WorldChunkGrain.WorldSizeX, visibleChunks[3].Id); // South
    }

    [Fact]
    public async Task GetVisibleChunks_ShouldReturnVisibleWorldChunksWhenChunkIsTopRight() {
        // Arrange
        long chunkId = WorldChunkGrain.WorldSizeX - 1;
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        VisibleWorldChunk[] visibleChunks = await grain.GetVisibleChunksById();

        // Assert
        Assert.Equal(4, visibleChunks.Length);
        Assert.Equal(chunkId, visibleChunks[0].Id); // Center
        Assert.Equal(chunkId + WorldChunkGrain.WorldSizeX, visibleChunks[1].Id); // South
        Assert.Equal(chunkId - 1 + WorldChunkGrain.WorldSizeX, visibleChunks[2].Id); // SouthWest
        Assert.Equal(chunkId - 1, visibleChunks[3].Id); // West
    }

    [Fact]
    public async Task GetVisibleChunks_ShouldReturnVisibleWorldChunksWhenChunkIsBottomRight() {
        // Arrange
        long chunkId = WorldChunkGrain.WorldSizeX * WorldChunkGrain.WorldSizeY - 1;
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        VisibleWorldChunk[] visibleChunks = await grain.GetVisibleChunksById();

        // Assert
        Assert.Equal(4, visibleChunks.Length);
        Assert.Equal(chunkId - WorldChunkGrain.WorldSizeX, visibleChunks[0].Id); // North
        Assert.Equal(chunkId, visibleChunks[1].Id); // Center
        Assert.Equal(chunkId - 1, visibleChunks[2].Id); // West
        Assert.Equal(chunkId - 1 - WorldChunkGrain.WorldSizeX, visibleChunks[3].Id); // NorthWest
    }

    [Fact]
    public async Task GetVisibleChunks_ShouldReturnVisibleWorldChunksWhenChunkIsCenter() {
        // Arrange
        long chunkId = (WorldChunkGrain.WorldSizeX / 2) * (WorldChunkGrain.WorldSizeY / 2);
        WorldChunkGrain grain = await Silo.CreateGrainAsync<WorldChunkGrain>(chunkId);

        // Act
        VisibleWorldChunk[] visibleChunks = await grain.GetVisibleChunksById();

        // Assert
        Assert.Equal(9, visibleChunks.Length);
        Assert.Equal(chunkId - WorldChunkGrain.WorldSizeX, visibleChunks[0].Id); // North
        Assert.Equal(chunkId + 1 - WorldChunkGrain.WorldSizeX, visibleChunks[1].Id); // NorthEast
        Assert.Equal(chunkId + 1, visibleChunks[2].Id); // East
        Assert.Equal(chunkId, visibleChunks[3].Id); // Center
        Assert.Equal(chunkId + 1 + WorldChunkGrain.WorldSizeX, visibleChunks[4].Id); // SouthEast
        Assert.Equal(chunkId + WorldChunkGrain.WorldSizeX, visibleChunks[5].Id); // South
        Assert.Equal(chunkId - 1 + WorldChunkGrain.WorldSizeX, visibleChunks[6].Id); // SouthWest
        Assert.Equal(chunkId - 1, visibleChunks[7].Id); // West
        Assert.Equal(chunkId - 1 - WorldChunkGrain.WorldSizeX, visibleChunks[8].Id); // NorthWest
    }
}