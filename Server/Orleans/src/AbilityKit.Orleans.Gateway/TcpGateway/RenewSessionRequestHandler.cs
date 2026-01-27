using AbilityKit.Orleans.Contracts.Accounts;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class RenewSessionRequestHandler : ITcpGatewayRequestHandler
{
    private sealed record WireRequest(string SessionToken, int ExtendSeconds, bool RotateToken);

    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;

    public RenewSessionRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry)
    {
        _clusterClient = clusterClient;
        _options = options;
        _registry = registry;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.RenewSessionOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var wire = TcpGatewayJson.Deserialize<WireRequest>(payload.Span);
        if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken))
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }

        var oldToken = wire.SessionToken;
        var oldBoundOther = _registry.TryGetConnectionIdByToken(oldToken, out var oldConnId) && oldConnId != context.ConnectionId;

        var session = _clusterClient.GetGrain<ISessionGrain>("global");
        var resp = await session.RenewAsync(new RenewSessionRequest(wire.SessionToken, wire.ExtendSeconds, wire.RotateToken));

        if (resp.IsValid && !string.IsNullOrWhiteSpace(resp.SessionToken))
        {
            if (wire.RotateToken)
            {
                _registry.UnbindToken(oldToken);
            }

            _registry.BindToken(resp.SessionToken, context.ConnectionId);

            if (oldBoundOther)
            {
                await _registry.TrySendKickAsync(oldToken, reason: "token_rotated", cancellationToken);
                _registry.UnbindToken(oldToken);
            }
        }

        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, TcpGatewayJson.Serialize(resp));
    }
}
