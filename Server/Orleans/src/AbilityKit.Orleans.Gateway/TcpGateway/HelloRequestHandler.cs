using System.Text;
using AbilityKit.Orleans.Contracts.Hello;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class HelloRequestHandler : ITcpGatewayRequestHandler
{
    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;

    public HelloRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options)
    {
        _clusterClient = clusterClient;
        _options = options;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.HelloOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var msg = Encoding.UTF8.GetString(payload.Span);
        var hello = _clusterClient.GetGrain<IHelloGrain>("default");
        var reply = await hello.SayHello(msg.Trim());
        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, Encoding.UTF8.GetBytes(reply));
    }
}
