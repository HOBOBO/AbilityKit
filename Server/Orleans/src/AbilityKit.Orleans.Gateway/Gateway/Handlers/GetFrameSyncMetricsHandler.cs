using AbilityKit.Orleans.Contracts.FrameSync;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[Core.GatewayHandler(OpCodes.GetMetricsRequest)]
public sealed partial class GetFrameSyncMetricsHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<GetFrameSyncMetricsHandler> _logger;

    public GetFrameSyncMetricsHandler(
        IClusterClient clusterClient,
        ILogger<GetFrameSyncMetricsHandler> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.AccountId)
            || string.IsNullOrWhiteSpace(context.SessionToken))
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);
        }

        try
        {
            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            var roomId = await mapping.TryGetAccountRoomAsync(context.AccountId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.NotFound);
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var snapshot = await room.GetSnapshotAsync();
            if (snapshot.Phase != RoomPhase.InBattle)
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.Forbidden);
            }

            var frameSync = _clusterClient.GetGrain<IBattleFrameSyncGrain>(roomId);
            var metrics = await frameSync.GetMetricsAsync();

            var wireMetrics = new WireFrameSyncMetrics(
                metrics.RoomId,
                metrics.WorldId,
                metrics.BattleId,
                metrics.CurrentFrame,
                metrics.TickRate,
                metrics.ObserverCount,
                metrics.AvgTickDeltaMs,
                metrics.LastTickDeltaMs,
                metrics.EffectiveHz,
                metrics.TotalInputsReceived,
                metrics.CatchUpHistoryFrames,
                metrics.RecordingFrameCount,
                metrics.UptimeSeconds);

            var payload = WireCustomBinary.Serialize(wireMetrics);
            return GatewayResponse.Ok(request.Seq, payload.Array ?? Array.Empty<byte>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetFrameSyncMetricsHandler] Error fetching metrics");
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }
}
