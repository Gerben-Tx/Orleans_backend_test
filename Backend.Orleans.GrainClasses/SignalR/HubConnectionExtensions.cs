using Microsoft.AspNetCore.SignalR.Client;

namespace Backend.Orleans.GrainClasses.SignalR;

public static partial class HubConnectionExtensions
{
    [HubClientProxy]
    public static partial IDisposable ClientRegistration<T>(this HubConnection connection, T provider);
    
    [HubServerProxy]
    public static partial T ServerProxy<T>(this HubConnection connection);
}
