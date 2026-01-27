using AbilityKit.Orleans.Contracts.Accounts;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class CreateSessionForAccountRequestHandler : ITcpGatewayRequestHandler
{
    private sealed record WireRequest(string AccountId, int ExpireSeconds, bool KickExisting);

    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;

    public CreateSessionForAccountRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry)
    {
        _clusterClient = clusterClient;
        _options = options;
        _registry = registry;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.CreateSessionForAccountOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var wire = TcpGatewayJson.Deserialize<WireRequest>(payload.Span);
        if (wire is null || string.IsNullOrWhiteSpace(wire.AccountId))
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }

        var session = _clusterClient.GetGrain<ISessionGrain>("global");
        var resp = await session.CreateSessionForAccountAsync(new CreateSessionForAccountRequest(wire.AccountId, wire.ExpireSeconds, wire.KickExisting));

        _registry.BindToken(resp.SessionToken, context.ConnectionId);

        if (!string.IsNullOrWhiteSpace(resp.KickedSessionToken))
        {
            await _registry.TrySendKickAsync(resp.KickedSessionToken, reason: "sso_kicked", cancellationToken);
            _registry.UnbindToken(resp.KickedSessionToken);
        }

        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, TcpGatewayJson.Serialize(resp));
    }
}
