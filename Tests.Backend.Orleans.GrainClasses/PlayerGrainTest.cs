using System.Numerics;
using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using Backend.Orleans.SharedContracts.Serialization;
using Backend.SignalR.SharedContracts;
using JetBrains.Annotations;
using Moq;
using Orleans.TestKit;
using Roy_T.AStar.Graphs;
using Roy_T.AStar.Grids;
using Roy_T.AStar.Paths;
using Roy_T.AStar.Primitives;
using Path = Roy_T.AStar.Paths.Path;

namespace Tests.Backend.Orleans.GrainClasses;

[TestSubject(typeof(PlayerGrain))]
public class PlayerGrainTest : TestKitBase {
    public PlayerGrainTest() { }

    [Fact]
    public async Task OnDeactivateAsync_ShouldLeaveTheCurrentChunk() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        long chunkId = 1L;
        string chunkGroupName = "Test Group";
        string connectionId = "Some Connection Id";

        // Create a mock chunk grain
        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.IsPlayerInChunk(It.IsAny<string>()))
            .Returns(Task.FromResult(true))
            .Verifiable(Times.Once);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(chunkGroupName))
            .Verifiable(Times.Exactly(1));
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([new VisibleWorldChunk(chunkId,new WorldChunkGrainPosition(0,0))]));

        // Create the realtime updates mock
        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.RemoveFromGroupAsync(chunkGroupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        // Set up the initial state
        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = chunkMock.Object,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        // Create the player grain
        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        await playerGrain.Initialize(connectionId, playerName); // Needed to initialize the _realtimeUpdatesConnectionId
        
        // Act
        await playerGrain.OnDeactivateAsync(new DeactivationReason(), CancellationToken.None);

        // Assert
        chunkMock.Verify(
            x => x.RemovePlayer(playerGrain.GetPrimaryKeyString(), initialState.Name),
            Times.Once);
        chunkMock.Verify();
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task Initialize_ShouldSetPlayerNameAndConnectionId() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string connectionId = "Connection123";
        long chunkId = 0L;
        string chunkGroupName = "Chunk Group";

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(chunkGroupName));
        chunkMock.Setup(x => x.AddPlayer(It.IsAny<string>(), playerName, It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()))
            .Returns(Task.CompletedTask);
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.AddToGroupAsync(chunkGroupName, connectionId))
            .Returns(Task.CompletedTask);

        PlayerState initialState = new PlayerState {
            Name = null!,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        await playerGrain.Initialize(connectionId, playerName);

        // Assert
        string actualName = await playerGrain.GetName();
        Assert.Equal(playerName, actualName);
    }

    [Fact]
    public async Task Initialize_ShouldNotOverwriteExistingPlayerName() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string existingPlayerName = "Existing Player";
        string newPlayerName = "New Player";
        string connectionId = "Connection123";
        long chunkId = 0L;
        string chunkGroupName = "Chunk Group";

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(chunkGroupName));
        chunkMock.Setup(x => x.AddPlayer(It.IsAny<string>(), existingPlayerName, It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()))
            .Returns(Task.CompletedTask);
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.AddToGroupAsync(chunkGroupName, connectionId))
            .Returns(Task.CompletedTask);

        PlayerState initialState = new PlayerState {
            Name = existingPlayerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        await playerGrain.Initialize(connectionId, newPlayerName);

        // Assert
        string actualName = await playerGrain.GetName();
        Assert.Equal(existingPlayerName, actualName);
    }

    [Fact]
    public async Task EnterChunk_ShouldMovePlayerToNewChunk() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string connectionId = "Connection123";
        long currentChunkId = 1L;
        long targetChunkId = 2L;
        string currentChunkGroupName = "Current Chunk Group";
        string targetChunkGroupName = "Target Chunk Group";

        Mock<IWorldChunkGrain> currentChunkMock = Silo.AddProbe<IWorldChunkGrain>(currentChunkId);
        currentChunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(currentChunkGroupName));
        currentChunkMock.Setup(x => x.RemovePlayer(It.IsAny<string>(), playerName))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2)); // Once due to Initialize, once due to EnterChunk
        currentChunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([new VisibleWorldChunk(currentChunkId,new WorldChunkGrainPosition(0,0))]));

        Mock<IWorldChunkGrain> targetChunkMock = Silo.AddProbe<IWorldChunkGrain>(targetChunkId);
        targetChunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(targetChunkGroupName));
        targetChunkMock.Setup(x => x.AddPlayer(It.IsAny<string>(), playerName, It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);
        targetChunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([new VisibleWorldChunk(targetChunkId,new WorldChunkGrainPosition(1,0))]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.RemoveFromGroupAsync(currentChunkGroupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2)); // Once due to Initialize, once due to EnterChunk
        realtimeUpdatesMock.Setup(x => x.AddToGroupAsync(targetChunkGroupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = currentChunkMock.Object,
            Position = new SerializableVector2(10, 20)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);
        await playerGrain.Initialize(connectionId, playerName);

        // Act
        await playerGrain.EnterChunk(targetChunkMock.Object);

        // Assert
        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();
        Assert.Equal(targetChunkMock.Object, currentChunk);
        currentChunkMock.Verify();
        targetChunkMock.Verify();
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task EnterChunk_ShouldNotChangeChunkIfAlreadyInTargetChunk() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        long chunkId = 1L;
        string chunkGroupName = "Chunk Group";

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(chunkGroupName));
        chunkMock.Verify(x => x.AddPlayer(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()), Times.Never);
        chunkMock.Setup(x => x.IsPlayerInChunk(It.IsAny<string>()))
            .Returns(Task.FromResult(true))
            .Verifiable(Times.Once);

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = chunkMock.Object,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);
        
        // No need for initialize() here since we don't care about the connection id

        // Act
        await playerGrain.EnterChunk(chunkMock.Object);

        // Assert
        chunkMock.Verify(x => x.RemovePlayer(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        chunkMock.Verify();
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task LeaveChunk_ShouldRemovePlayerFromChunk() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string connectionId = "Connection123";
        long chunkId = 1L;
        string chunkGroupName = "Chunk Group";

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(chunkGroupName));
        chunkMock.Setup(x => x.RemovePlayer(It.IsAny<string>(), playerName))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2)); // Once due to Initialize, once due to LeaveChunk
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([new VisibleWorldChunk(chunkId,new WorldChunkGrainPosition(0,0))]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.RemoveFromGroupAsync(chunkGroupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2)); // Once due to Initialize, once due to LeaveChunk

        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = chunkMock.Object,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);
        await playerGrain.Initialize(connectionId, playerName);

        // Act
        await playerGrain.LeaveChunk(chunkMock.Object);

        // Assert
        chunkMock.Verify();
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task GetCurrentChunk_ShouldReturnChunkFromState() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        long chunkId = 5L;

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = chunkMock.Object,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();

        // Assert
        Assert.Equal(chunkMock.Object, currentChunk);
    }

    [Fact]
    public async Task GetCurrentChunk_ShouldReturnDefaultChunkWhenStateIsNull() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";

        Mock<IWorldChunkGrain> defaultChunkMock = Silo.AddProbe<IWorldChunkGrain>(0L);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            ChunkGrain = null,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        IWorldChunkGrain currentChunk = await playerGrain.GetCurrentChunk();

        // Assert
        Assert.Equal(defaultChunkMock.Object, currentChunk);
    }

    [Fact]
    public async Task GetPosition_ShouldReturnPlayerPosition() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        SerializableVector2 expectedPosition = new SerializableVector2(100, 200);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = expectedPosition
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        SerializableVector2 actualPosition = await playerGrain.GetPosition();

        // Assert
        Assert.Equal(expectedPosition.X, actualPosition.X);
        Assert.Equal(expectedPosition.Y, actualPosition.Y);
    }

    [Fact]
    public async Task GetName_ShouldReturnPlayerName() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string expectedName = "Test Player";

        PlayerState initialState = new PlayerState {
            Name = expectedName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        string actualName = await playerGrain.GetName();

        // Assert
        Assert.Equal(expectedName, actualName);
    }

    [Fact]
    public async Task JoinRealtimeUpdatesGroup_ShouldAddConnectionToGroup() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string connectionId = "Connection123";
        string groupName = "Test Group";
        long chunkId = 0L;

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(groupName));
        chunkMock.Setup(x => x.AddPlayer(It.IsAny<string>(), playerName, It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()))
            .Returns(Task.CompletedTask);
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.AddToGroupAsync(groupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce);

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);
        await playerGrain.Initialize(connectionId, playerName);

        // Act
        await playerGrain.JoinRealtimeUpdatesGroup(groupName);

        // Assert
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task JoinRealtimeUpdatesGroup_ShouldDoNothingWhenConnectionIdIsNull() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string groupName = "Test Group";

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        await playerGrain.JoinRealtimeUpdatesGroup(groupName);

        // Assert
        realtimeUpdatesMock.Verify(
            x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task LeaveRealtimeUpdatesGroup_ShouldRemoveConnectionFromGroup() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string connectionId = "Connection123";
        string groupName = "Test Group";
        long chunkId = 0L;

        Mock<IWorldChunkGrain> chunkMock = Silo.AddProbe<IWorldChunkGrain>(chunkId);
        chunkMock.Setup(x => x.GetRealtimeUpdatesGroupName())
            .Returns(Task.FromResult(groupName));
        chunkMock.Setup(x => x.AddPlayer(It.IsAny<string>(), playerName, It.IsAny<SerializableVector2>(), It.IsAny<Queue<SerializableVector2>>()))
            .Returns(Task.CompletedTask);
        chunkMock.Setup(x => x.GetVisibleChunks(It.IsAny<int>()))
            .Returns(Task.FromResult<VisibleWorldChunk[]>([new VisibleWorldChunk(chunkId,new WorldChunkGrainPosition(0,0))]));

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();
        realtimeUpdatesMock.Setup(x => x.AddToGroupAsync(groupName, connectionId))
            .Returns(Task.CompletedTask);
        realtimeUpdatesMock.Setup(x => x.RemoveFromGroupAsync(groupName, connectionId))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2)); // Once due to Initialize, once due to LeaveRealtimeUpdatesGroup

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);
        await playerGrain.Initialize(connectionId, playerName);

        // Act
        await playerGrain.LeaveRealtimeUpdatesGroup(groupName);

        // Assert
        realtimeUpdatesMock.Verify();
    }

    [Fact]
    public async Task LeaveRealtimeUpdatesGroup_ShouldDoNothingWhenConnectionIdIsNull() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";
        string groupName = "Test Group";

        Mock<IRealtimeUpdatesOrleans> realtimeUpdatesMock = Silo.AddServiceProbe<IRealtimeUpdatesOrleans>();

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        await playerGrain.LeaveRealtimeUpdatesGroup(groupName);

        // Assert
        realtimeUpdatesMock.Verify(
            x => x.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task DeactivateOnIdle_ShouldComplete() {
        // Arrange
        Guid playerGrainGuid = Guid.NewGuid();
        string playerName = "Test Player";

        PlayerState initialState = new PlayerState {
            Name = playerName,
            Position = new SerializableVector2(0, 0)
        };
        Silo.AddPersistentState("player", "tableStore", initialState);

        PlayerGrain playerGrain = await Silo.CreateGrainAsync<PlayerGrain>(playerGrainGuid);

        // Act
        Task result = playerGrain.DeactivateOnIdle();

        // Assert
        Assert.True(result.IsCompleted);
    }
}