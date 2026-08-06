using System.Collections.Generic;
using AbilityKit.Protocol;
using MemoryPack;

namespace AbilityKit.Protocol.Room
{
    public static class ReliableBattleEventGatewayOpCodes
    {
        public const uint Ack = RoomGatewayOpCodes.AckReliableBattleEvents;
        public const uint EventsPushed = RoomGatewayOpCodes.ReliableBattleEventsPushed;
    }

    [MemoryPackable]
    public partial struct WireReliableBattleEvent
    {
        [MemoryPackOrder(0)] public string EventId { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public string Epoch { get; set; }
        [MemoryPackOrder(3)] public long Sequence { get; set; }
        [MemoryPackOrder(4)] public int SourceFrame { get; set; }
        [MemoryPackOrder(5)] public int EventType { get; set; }
        [MemoryPackOrder(6)] public byte[]? Payload { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.ReliableBattleEventsPushed, ProtocolDirection.ServerToClient, nameof(WireReliableBattleEventPush))]
    [MemoryPackable]
    public partial struct WireReliableBattleEventPush
    {
        [MemoryPackOrder(0)] public string BattleId { get; set; }
        [MemoryPackOrder(1)] public string Epoch { get; set; }
        [MemoryPackOrder(2)] public long FirstAvailableSequence { get; set; }
        [MemoryPackOrder(3)] public long Watermark { get; set; }
        [MemoryPackOrder(4)] public bool RetentionGap { get; set; }
        [MemoryPackOrder(5)] public List<WireReliableBattleEvent>? Events { get; set; }
    }

    [ProtocolOpCode(RoomGatewayOpCodes.AckReliableBattleEvents, ProtocolDirection.ClientToServer, nameof(WireAckReliableBattleEventsReq))]
    [MemoryPackable]
    public partial struct WireAckReliableBattleEventsReq
    {
        [MemoryPackOrder(0)] public string SessionToken { get; set; }
        [MemoryPackOrder(1)] public string BattleId { get; set; }
        [MemoryPackOrder(2)] public string RoomId { get; set; }
        [MemoryPackOrder(3)] public string Epoch { get; set; }
        [MemoryPackOrder(4)] public long AckSequence { get; set; }
    }

    [MemoryPackable]
    public partial struct WireAckReliableBattleEventsRes
    {
        [MemoryPackOrder(0)] public bool Success { get; set; }
        [MemoryPackOrder(1)] public long AcceptedAckSequence { get; set; }
        [MemoryPackOrder(2)] public string Message { get; set; }
    }
}
