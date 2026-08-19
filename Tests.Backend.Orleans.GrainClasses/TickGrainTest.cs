using Backend.Orleans.GrainClasses;
using Backend.Orleans.SharedContracts;
using JetBrains.Annotations;
using Moq;
using Orleans.TestKit;

namespace Tests.Backend.Orleans.GrainClasses;

[TestSubject(typeof(TickGrain))]
public class TickGrainTest : TestKitBase {
    [Fact]
    public async Task GetTicks_ShouldReturnTicks() {
        // Arrange
        ulong expectedTicks = 1234567890;

        Mock<ITickManager> tickManagerMock = Silo.AddServiceProbe<ITickManager>();
        tickManagerMock.Setup(tm => tm.GetTicks()).Returns(expectedTicks);

        TickGrain grain = await Silo.CreateGrainAsync<TickGrain>(1L);

        // Act
        ulong ticks = await grain.GetTicks();

        // Assert
        Assert.Equal(ticks, expectedTicks);
    }

    [Fact]
    public async Task GetTicksPerSecond_ShouldReturnTicksPerSecond() {
        // Arrange
        uint expectedTicksPerSecond = 12;

        Mock<ITickManager> tickManagerMock = Silo.AddServiceProbe<ITickManager>();
        tickManagerMock.Setup(tm => tm.GetTicksPerSecond()).Returns(expectedTicksPerSecond);

        TickGrain grain = await Silo.CreateGrainAsync<TickGrain>(1L);

        // Act
        uint ticksPerSecond = await grain.GetTicksPerSecond();

        // Assert
        Assert.Equal(ticksPerSecond, expectedTicksPerSecond);
    }
}