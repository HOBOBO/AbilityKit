using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Rooms;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class JoinRoomRequestHandler : ITcpGatewayRequestHandler
{
    private sealed record WireRequest(string SessionToken, string Region, string ServerId, string RoomId);

    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;

    public JoinRoomRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry)
    {
        _clusterClient = clusterClient;
        _options = options;
        _registry = registry;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.JoinRoomOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var wire = TcpGatewayJson.Deserialize<WireRequest>(payload.Span);
        if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken) || string.IsNullOrWhiteSpace(wire.RoomId))
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }

        var session = _clusterClient.GetGrain<ISessionGrain>("global");
        var v = await session.ValidateAsync(new ValidateSessionRequest(wire.SessionToken));
        if (!v.IsValid || string.IsNullOrWhiteSpace(v.AccountId))
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }

        _registry.BindToken(wire.SessionToken, context.ConnectionId);

        var room = _clusterClient.GetGrain<IRoomGrain>(wire.RoomId);
        await room.JoinAsync(v.AccountId);

        var snapshot = await room.GetSnapshotAsync();
        return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, TcpGatewayJson.Serialize(snapshot));
    }
}
