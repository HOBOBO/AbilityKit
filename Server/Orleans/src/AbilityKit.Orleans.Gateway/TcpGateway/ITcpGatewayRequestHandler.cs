namespace AbilityKit.Orleans.Gateway.TcpGateway;

public interface ITcpGatewayRequestHandler
{
    bool CanHandle(uint opCode);

    Task<TcpGatewayResponseEnvelope> HandleAsync(NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
