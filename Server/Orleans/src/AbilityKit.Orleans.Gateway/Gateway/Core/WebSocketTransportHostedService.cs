using AbilityKit.Orleans.Gateway.Networking;
using Microsoft.Extensions.Hosting;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed class WebSocketTransportHostedService : BackgroundService
{
    private readonly WebSocketTransportServer _transportServer;

    public WebSocketTransportHostedService(WebSocketTransportServer transportServer)
    {
        _transportServer = transportServer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _transportServer.StartAsync(stoppingToken);
    }
}
