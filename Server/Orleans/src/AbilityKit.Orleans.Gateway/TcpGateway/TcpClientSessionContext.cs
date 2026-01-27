namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class TcpClientSessionContext
{
    public long ConnectionId { get; }

    public TcpClientSessionContext(long connectionId)
    {
        ConnectionId = connectionId;
    }
}
