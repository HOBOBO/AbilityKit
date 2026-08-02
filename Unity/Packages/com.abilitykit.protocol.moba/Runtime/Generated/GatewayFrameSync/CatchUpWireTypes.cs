using MemoryPack;

namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    [MemoryPackable]
    public readonly partial struct WireCatchUpRequest
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly int FromFrameExclusive;
        [MemoryPackOrder(3)] public readonly int ToFrameInclusive;

        public WireCatchUpRequest(ulong roomId, ulong worldId, int fromFrameExclusive, int toFrameInclusive)
        {
            RoomId = roomId;
            WorldId = worldId;
            FromFrameExclusive = fromFrameExclusive;
            ToFrameInclusive = toFrameInclusive;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireCatchUpFrame
    {
        [MemoryPackOrder(0)] public readonly int Frame;
        [MemoryPackOrder(1)] public readonly WireInputItem[] Inputs;

        [MemoryPackConstructor]
        public WireCatchUpFrame(int frame, WireInputItem[] inputs)
        {
            Frame = frame;
            Inputs = inputs;
        }
    }

    [MemoryPackable]
    public readonly partial struct WireCatchUpPayloadPush
    {
        [MemoryPackOrder(0)] public readonly ulong RoomId;
        [MemoryPackOrder(1)] public readonly ulong WorldId;
        [MemoryPackOrder(2)] public readonly int StartFrame;
        [MemoryPackOrder(3)] public readonly WireCatchUpFrame[] Frames;

        [MemoryPackConstructor]
        public WireCatchUpPayloadPush(ulong roomId, ulong worldId, int startFrame, WireCatchUpFrame[] frames)
        {
            RoomId = roomId;
            WorldId = worldId;
            StartFrame = startFrame;
            Frames = frames;
        }
    }
}
