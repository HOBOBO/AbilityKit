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

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var hello = _clusterClient.GetGrain<IHelloGrain>("default");
        var msg = TcpGatewayJson.Deserialize<string>(payload.Span) ?? string.Empty;
        var reply = await hello.SayHello(msg);
        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, TcpGatewayJson.Serialize(reply));
    }
}
