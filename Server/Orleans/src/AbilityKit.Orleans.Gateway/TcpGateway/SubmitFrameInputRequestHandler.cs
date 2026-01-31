using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Orleans.Contracts.FrameSync;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public sealed class SubmitFrameInputRequestHandler : ITcpGatewayRequestHandler
{
    private readonly IOptions<TcpGatewayOptions> _options;
    private readonly ITcpGatewaySessionRegistry _registry;
    private readonly IClusterClient _clusterClient;
    private readonly FrameSyncObserverHub _observerHub;

    public SubmitFrameInputRequestHandler(IOptions<TcpGatewayOptions> options, ITcpGatewaySessionRegistry registry, IClusterClient clusterClient, FrameSyncObserverHub observerHub)
    {
        _options = options;
        _registry = registry;
        _clusterClient = clusterClient;
        _observerHub = observerHub;
    }

    public bool CanHandle(uint opCode) => opCode == _options.Value.SubmitFrameInputOpCode;

    public async Task<TcpGatewayResponseEnvelope> HandleAsync(TcpClientSessionContext context, NetworkPacketHeader header, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        // Stage-1: only decode + ack. No battle logic is wired yet.
        try
        {
            var req = WireCustomBinary.DeserializeSubmitFrameInputReq(payload);

            // Stage-4: forward to BattleFrameSyncGrain; Gateway receives frame events via FrameSyncObserverHub and broadcasts.
            if (_clusterClient != null)
            {
                _ = _observerHub?.EnsureSubscribedAsync(req.RoomId, cancellationToken);

                var grain = _clusterClient.GetGrain<IBattleFrameSyncGrain>(req.RoomId.ToString());
                await grain.SubmitInputAsync(
                    worldId: req.WorldId,
                    frame: req.Frame,
                    input: new FrameInputItem(req.PlayerId, req.InputOpCode, req.InputPayload));
            }

            var resp = new WireSubmitFrameInputRes(
                accepted: true,
                serverFrame: 0,
                reasonCode: 0);

            var respPayload = WireCustomBinary.Serialize(in resp);
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.Ok, respPayload);
        }
        catch
        {
            return new TcpGatewayResponseEnvelope(TcpGatewayStatusCode.BadRequest, ReadOnlyMemory<byte>.Empty);
        }
    }
}
