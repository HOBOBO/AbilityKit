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

/// <summary>
/// 为已鉴权的本地主控声明输入代际，仍使用固定基线快照和可靠事件游标。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.SubscribeScopedStateSync)]
public sealed partial class SubscribeScopedStateSyncHandler(SubscribeStateSyncHandler legacyHandler) : GatewayRequestHandlerBase
{
    public override ValueTask<GatewayResponse> HandleAsync(GatewayRequest request, GatewaySessionContext context,
        CancellationToken cancellationToken)
        => Subscribe(legacyHandler, request, context, receivesMovementProof: false);

    internal static ValueTask<GatewayResponse> Subscribe(SubscribeStateSyncHandler legacyHandler,
        GatewayRequest request, GatewaySessionContext context, bool receivesMovementProof)
    {
        if (request.Payload is not { Length: > 0 })
            return ValueTask.FromResult(GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest));

        var scoped = WireRoomGatewayBinary.Deserialize<WireSubscribeScopedStateSyncReq>(request.Payload);
        return scoped.InputGeneration == Guid.Empty
            ? ValueTask.FromResult(GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest))
            : legacyHandler.SubscribeAsync(request.Seq, context, scoped.Subscription, fixedBaseline: true,
                scoped.InputGeneration, scoped.WorldId, scoped.PlayerId, receivesMovementProof);
    }
}

/// <summary>
/// 使用有来源的状态同步输入，并明确接受携带同修订移动证明的快照。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.SubscribeStateSyncMovementProof)]
public sealed partial class SubscribeStateSyncMovementProofHandler(SubscribeStateSyncHandler legacyHandler)
    : GatewayRequestHandlerBase
{
    public override ValueTask<GatewayResponse> HandleAsync(GatewayRequest request, GatewaySessionContext context,
        CancellationToken cancellationToken) => SubscribeScopedStateSyncHandler.Subscribe(
            legacyHandler, request, context, receivesMovementProof: true);
}
