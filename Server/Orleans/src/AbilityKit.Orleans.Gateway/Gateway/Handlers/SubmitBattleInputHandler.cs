using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Options;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 閫氱敤鎴樻枟杈撳叆鎻愪氦 Handler銆俻ayload 鐢卞叿浣撶帺娉曞崗璁В閲婏紝Gateway 鍙礋璐ｉ壌鏉冧笌杞彂銆?
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.SubmitBattleInput)]
public sealed partial class SubmitBattleInputHandler : GatewayRequestHandlerBase
{
    private const int MaxFutureLeadFrames = 120;
    private readonly IClusterClient _clusterClient;
    private readonly GatewayBattleInputGuard _inputGuard;
    private readonly BattleInputSecurityOptions _inputSecurityOptions;

    public SubmitBattleInputHandler(
        IClusterClient clusterClient,
        GatewayBattleInputGuard inputGuard,
        IOptions<BattleInputSecurityOptions> inputSecurityOptions)
    {
        _clusterClient = clusterClient;
        _inputGuard = inputGuard;
        _inputSecurityOptions = inputSecurityOptions.Value.Snapshot();
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        var req = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken)
            || string.IsNullOrWhiteSpace(req.BattleId)
            || req.WorldId == 0
            || req.Frame < 0
            || req.PlayerId == 0
            || req.InputOpCode <= 0
            || req.InputOpCode > _inputSecurityOptions.MaxOpCode
            || (req.Payload?.Length ?? 0) > _inputSecurityOptions.MaxPayloadBytes)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            var roomId = await mapping.TryGetAccountRoomAsync(accountId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var snapshot = await room.GetSnapshotAsync();
            if (!CanSubmitInput(snapshot, req.BattleId, req.WorldId, accountId, req.PlayerId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            var guard = _inputGuard.Check(req.SessionToken, req.BattleId, req.PlayerId, req.CommandSequence, DateTime.UtcNow.Ticks);
            if (guard == GatewayBattleInputGuardResult.Duplicate)
            {
                return CreateInputResponse(request.Seq, true, req.Frame, req.Frame, "Deduplicated", "Command sequence was already accepted.", shouldResync: false);
            }

            if (guard == GatewayBattleInputGuardResult.TooOld)
            {
                return CreateInputResponse(request.Seq, false, req.Frame, req.Frame, BattleResultStatusCodes.RejectedSequenceTooOld, "Command sequence is outside the replay window.", shouldResync: false);
            }

            if (guard == GatewayBattleInputGuardResult.RateLimited)
            {
                return CreateInputResponse(request.Seq, false, req.Frame, req.Frame, BattleResultStatusCodes.RejectedRateLimited, "Battle input rate limit exceeded.", shouldResync: false);
            }

            var battle = _clusterClient.GetGrain<IBattleLogicHostGrain>(req.BattleId);
            var currentFrame = await battle.GetCurrentFrameAsync();
            if (req.Frame > currentFrame + MaxFutureLeadFrames)
            {
                return CreateInputResponse(request.Seq, false, req.Frame, currentFrame, "RejectedTooFarFuture", "Input frame is too far ahead of the battle frame.", shouldResync: true);
            }

            var submit = await battle.SubmitInputAsync(req.WorldId, req.Frame, new BattleInputItem
            {
                PlayerId = req.PlayerId,
                OpCode = req.InputOpCode,
                Payload = req.Payload ?? Array.Empty<byte>(),
                CommandSequence = req.CommandSequence
            });
            if (submit.Accepted)
            {
                _inputGuard.RecordAccepted(req.SessionToken, req.BattleId, req.PlayerId, req.CommandSequence);
            }

            context.AccountId = accountId;
            var wire = new WireSubmitBattleInputRes
            {
                Success = submit.Accepted,
                AcceptedFrame = submit.AcceptedFrame,
                Message = submit.Message,
                CurrentFrame = submit.CurrentFrame,
                Status = submit.Status,
                // 限速/重复/容量是瞬时限流，不是状态分歧：丢弃该输入即可，
                // 不应触发客户端全量重同步（会封锁本地预测输入，表现为"无法控制"）。
                ShouldResync = !submit.Accepted && IsStateDivergenceStatus(submit.Status),
                ServerTicks = DateTime.UtcNow.Ticks
            };
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }

    /// <summary>
    /// 只有反映真实状态分歧的拒绝（世界/玩家不匹配、载荷损坏、输入缓冲拒绝）才要求
    /// 客户端全量重同步；瞬时限流（限速/重复/序列过旧）与未初始化只丢弃该输入。
    /// 误把限流当作分歧会驱动客户端进入恢复态并封锁本地预测输入（表现为"无法控制"）。
    /// </summary>
    private static bool IsStateDivergenceStatus(string? status)
    {
        return status == BattleResultStatusCodes.RejectedWorldMismatch ||
            status == BattleResultStatusCodes.RejectedInvalidPayload ||
            status == BattleResultStatusCodes.RejectedInvalidOpCode ||
            status == BattleResultStatusCodes.RejectedInvalidPlayer ||
            status == BattleResultStatusCodes.RejectedNullPlayer ||
            status == BattleResultStatusCodes.RejectedByInputBuffer;
    }

    private static GatewayResponse CreateInputResponse(
        uint requestSequence,
        bool success,
        int acceptedFrame,
        int currentFrame,
        string status,
        string message,
        bool shouldResync)
    {
        var wire = new WireSubmitBattleInputRes
        {
            Success = success,
            AcceptedFrame = acceptedFrame,
            Message = message,
            CurrentFrame = currentFrame,
            Status = status,
            ShouldResync = shouldResync,
            ServerTicks = DateTime.UtcNow.Ticks
        };
        return GatewayResponse.Ok(requestSequence, WireRoomGatewayBinary.Serialize(in wire).ToArray());
    }

    internal static bool CanSubmitInput(RoomSnapshot? snapshot, string? battleId, ulong worldId, string? accountId, uint playerId)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(battleId) || worldId == 0 || string.IsNullOrWhiteSpace(accountId) || playerId == 0)
        {
            return false;
        }

        if (!string.Equals(snapshot.BattleId, battleId, StringComparison.Ordinal) || snapshot.WorldId != worldId)
        {
            return false;
        }

        return RoomGatewayWireMapper.ResolvePlayerId(snapshot, accountId) == playerId;
    }
}

