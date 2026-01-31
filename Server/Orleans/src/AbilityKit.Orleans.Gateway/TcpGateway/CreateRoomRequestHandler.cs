using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Contracts.Rooms;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class CreateRoomRequestHandler : ITcpGatewayRequestHandler
{
    private sealed record WireRequest(
        string SessionToken,
        string Region,
        string ServerId,
        string RoomType,
        string Title,
        bool IsPublic,
        int MaxPlayers,
        IReadOnlyDictionary<string, string>? Tags);

    private readonly IClusterClient _clusterClient;
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;

    public CreateRoomRequestHandler(IClusterClient clusterClient, IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry)
    {
        _clusterClient = clusterClient;
        _options = options;
        _registry = registry;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.CreateRoomOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        var wire = TcpGatewayJson.Deserialize<WireRequest>(payload.Span);
        if (wire is null || string.IsNullOrWhiteSpace(wire.SessionToken))
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

        var directoryKey = $"{wire.Region}:{wire.ServerId}";
        var directory = _clusterClient.GetGrain<IRoomDirectoryGrain>(directoryKey);

        var req = new CreateRoomRequest(
            v.AccountId,
            wire.Region,
            wire.ServerId,
            wire.RoomType,
            wire.Title,
            wire.IsPublic,
            wire.MaxPlayers,
            wire.Tags == null ? null : new Dictionary<string, string>(wire.Tags));

        var resp = await directory.CreateRoomAsync(req);

        // Ensure numeric roomId mapping exists for frame-sync (wire uses numeric roomId, room grain uses string roomId).
        var mapper = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
        var numericRoomId = await mapper.GetOrCreateNumericIdAsync(resp.RoomId);

        return new TcpGatewayResponseEnvelope(
            TcpGatewayStatusCode.Ok,
            TcpGatewayJson.Serialize(new { resp.RoomId, NumericRoomId = numericRoomId }));
    }
}
