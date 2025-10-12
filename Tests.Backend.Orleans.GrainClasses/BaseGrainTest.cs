using Backend.Orleans.GrainClasses;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Moq;

namespace Tests.Backend.Orleans.GrainClasses;

[TestSubject(typeof(BaseGrain))]
public class BaseGrainTest {
    private readonly Mock<ILogger<BaseGrain>> _loggerMock;
    private readonly BaseGrain _grain;

    public BaseGrainTest() {
        _loggerMock = new Mock<ILogger<BaseGrain>>();
        _grain = new BaseGrain(_loggerMock.Object);
    }

    [Fact]
    public async Task OnActivateAsync_ShouldCallLoggerDebug() {
        // Arrange
        var cancellationToken = CancellationToken.None;
    
        // Act
        await _grain.OnActivateAsync(cancellationToken);
    
        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OnActivateAsync")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDeactivateAsync_ShouldCallLoggerDebugWithReason() {
        // Arrange
        var reason = new DeactivationReason(DeactivationReasonCode.ActivationIdle, "Test reason");
        var cancellationToken = CancellationToken.None;

        // Act
        await _grain.OnDeactivateAsync(reason, cancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("OnDeactivateAsync") && v.ToString()!.Contains("Test reason")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}