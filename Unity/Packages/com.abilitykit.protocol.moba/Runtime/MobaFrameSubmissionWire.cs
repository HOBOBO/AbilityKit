using System;
using MemoryPack;

namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    /// <summary>
    /// 与结算订阅配套的 MOBA 输入提交与指标查询操作码。
    /// </summary>
    public static class OpCodes
    {
        public const uint SubmitFrameInput = 2001;
        public const uint GetMetricsRequest = 2003;
        public const uint MetricsResponse = GetMetricsRequest;
    }

    [MemoryPackable]
    public readonly partial struct WireSubmitFrameInputReq
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly uint PlayerId;
        [MemoryPackOrder(3)] public readonly int Frame;
        [MemoryPackOrder(4)] public readonly int InputOpCode;
        [MemoryPackOrder(5)] public readonly byte[] InputPayload;

        [MemoryPackConstructor]
        public WireSubmitFrameInputReq(ulong roomId, ulong worldId, uint playerId, int frame, int inputOpCode, byte[] inputPayload)
        {
            RoomId = roomId;
            WorldId = worldId;
            PlayerId = playerId;
            Frame = frame;
            InputOpCode = inputOpCode;
            InputPayload = inputPayload;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireSubmitFrameInputRes
    {
        [MemoryPackOrder(0)] public readonly bool Accepted;
        [MemoryPackOrder(1)] public readonly int ServerFrame;
        [MemoryPackOrder(2)] public readonly int ReasonCode;

        public WireSubmitFrameInputRes(bool accepted, int serverFrame, int reasonCode)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            ReasonCode = reasonCode;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireFrameSyncMetrics
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly string? BattleId;
        [MemoryPackOrder(3)] public readonly int CurrentFrame;
        [MemoryPackOrder(4)] public readonly int TickRate;
        [MemoryPackOrder(5)] public readonly int ObserverCount;
        [MemoryPackOrder(6)] public readonly double AvgTickDeltaMs;
        [MemoryPackOrder(7)] public readonly double LastTickDeltaMs;
        [MemoryPackOrder(8)] public readonly double EffectiveHz;
        [MemoryPackOrder(9)] public readonly int TotalFramesReceived;
        [MemoryPackOrder(10)] public readonly int CatchUpHistoryFrames;
        [MemoryPackOrder(11)] public readonly int RecordingFrameCount;
        [MemoryPackOrder(12)] public readonly long UptimeSeconds;

        [MemoryPackConstructor]
        public WireFrameSyncMetrics(
            ulong roomId, ulong worldId, string? battleId,
            int currentFrame, int tickRate, int observerCount,
            double avgTickDeltaMs, double lastTickDeltaMs, double effectiveHz,
            int totalFramesReceived, int catchUpHistoryFrames,
            int recordingFrameCount, long uptimeSeconds)
        {
            RoomId = roomId;
            WorldId = worldId;
            BattleId = battleId;
            CurrentFrame = currentFrame;
            TickRate = tickRate;
            ObserverCount = observerCount;
            AvgTickDeltaMs = avgTickDeltaMs;
            LastTickDeltaMs = lastTickDeltaMs;
            EffectiveHz = effectiveHz;
            TotalFramesReceived = totalFramesReceived;
            CatchUpHistoryFrames = catchUpHistoryFrames;
            RecordingFrameCount = recordingFrameCount;
            UptimeSeconds = uptimeSeconds;
        }
    }

    public static class WireCustomBinary
    {
        public static ArraySegment<byte> Serialize(in WireSubmitFrameInputReq req)
        {
            return ToSegment(MemoryPackSerializer.Serialize(req));
        }

        public static ArraySegment<byte> Serialize(in WireSubmitFrameInputRes res)
        {
            return ToSegment(MemoryPackSerializer.Serialize(res));
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ArraySegment<byte> payload)
        {
            return DeserializeSubmitFrameInputReq(ToSpan(payload));
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ReadOnlyMemory<byte> payload)
        {
            return DeserializeSubmitFrameInputReq(payload.Span);
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ReadOnlySpan<byte> payload)
        {
            return payload.Length == 0
                ? default
                : MemoryPackSerializer.Deserialize<WireSubmitFrameInputReq>(payload);
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ArraySegment<byte> payload)
        {
            return DeserializeSubmitFrameInputRes(ToSpan(payload));
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ReadOnlyMemory<byte> payload)
        {
            return DeserializeSubmitFrameInputRes(payload.Span);
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ReadOnlySpan<byte> payload)
        {
            return payload.Length == 0
                ? default
                : MemoryPackSerializer.Deserialize<WireSubmitFrameInputRes>(payload);
        }

        public static ArraySegment<byte> Serialize(in WireFrameSyncMetrics metrics)
        {
            return ToSegment(MemoryPackSerializer.Serialize(metrics));
        }

        public static WireFrameSyncMetrics DeserializeMetrics(ArraySegment<byte> payload)
        {
            return DeserializeMetrics(ToSpan(payload));
        }

        public static WireFrameSyncMetrics DeserializeMetrics(ReadOnlyMemory<byte> payload)
        {
            return DeserializeMetrics(payload.Span);
        }

        public static WireFrameSyncMetrics DeserializeMetrics(ReadOnlySpan<byte> payload)
        {
            return payload.Length == 0
                ? default
                : MemoryPackSerializer.Deserialize<WireFrameSyncMetrics>(payload);
        }

        private static ArraySegment<byte> ToSegment(byte[] bytes)
        {
            return new ArraySegment<byte>(bytes ?? Array.Empty<byte>());
        }

        private static ReadOnlySpan<byte> ToSpan(ArraySegment<byte> payload)
        {
            return payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count);
        }
    }
}
