namespace AbilityKit.Orleans.Gateway.TcpGateway;

public interface ITcpGatewayRequestHandler
{
    bool CanHandle(uint opCode);

    Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
