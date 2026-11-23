using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 打开固定基线快照流，使用与旧入口相同的鉴权和可靠事件订阅。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.SubscribeFixedBaselineStateSync)]
public sealed partial class SubscribeFixedBaselineStateSyncHandler(SubscribeStateSyncHandler legacyHandler)
    : GatewayRequestHandlerBase
{
    public override ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request, GatewaySessionContext context, CancellationToken cancellationToken)
        => legacyHandler.SubscribeAsync(request, context, fixedBaseline: true);
}
