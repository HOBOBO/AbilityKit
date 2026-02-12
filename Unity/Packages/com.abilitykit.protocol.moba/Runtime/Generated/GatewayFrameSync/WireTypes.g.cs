using MemoryPack;

namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    [MemoryPackable]
    public readonly partial struct WireInputItem
    {
        [MemoryPackOrder(0)] public readonly uint PlayerId;
        [MemoryPackOrder(1)] public readonly int OpCode;
        [MemoryPackOrder(2)] public readonly byte[] Payload;

        [MemoryPackConstructor]
        public WireInputItem(uint playerId, int opCode, byte[] payload)
        {
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
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
    public readonly partial struct WireFramePushedPush
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly int Frame;
        [MemoryPackOrder(3)] public readonly WireInputItem[] Inputs;

        [MemoryPackConstructor]
        public WireFramePushedPush(ulong roomId, ulong worldId, int frame, WireInputItem[] inputs)
        {
            RoomId = roomId;
            WorldId = worldId;
            Frame = frame;
            Inputs = inputs;
        }
    }
}
