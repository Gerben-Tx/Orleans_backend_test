using Microsoft.Extensions.Logging;

namespace Backend.Orleans.GrainClasses;

public class BaseGrain : Grain {
    private readonly ILogger<BaseGrain> _logger;
    
    public BaseGrain(
        ILogger<BaseGrain> logger
    ) {
        _logger = logger;
    }
    
    public override Task OnActivateAsync(CancellationToken cancellationToken) {
        _logger.LogDebug("OnActivateAsync");
        
        return base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken) {
        _logger.LogDebug("OnDeactivateAsync, Reason: {reason}", reason);
        
        return base.OnDeactivateAsync(reason, cancellationToken);
    }
}