using AbilityKit.Orleans.Contracts.Accounts;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class LogoutRequestHandler : ITcpGatewayRequestHandler
{
    private sealed record WireRequest(string SessionToken);

    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;

    public LogoutRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry)
    {
        _clusterClient = clusterClient;
        _options = options;
        _registry = registry;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.LogoutOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var wire = TcpGatewayJson.Deserialize<WireRequest>(payload.Span);
        if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken))
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }

        var session = _clusterClient.GetGrain<ISessionGrain>("global");
        var resp = await session.LogoutAsync(new LogoutRequest(wire.SessionToken));

        _registry.UnbindToken(wire.SessionToken);

        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, TcpGatewayJson.Serialize(resp));
    }
}
