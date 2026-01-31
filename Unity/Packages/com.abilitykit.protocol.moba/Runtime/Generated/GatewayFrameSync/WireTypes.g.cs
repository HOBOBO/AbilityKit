namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    public readonly struct WireInputItem
    {
        public readonly uint PlayerId;
        public readonly int OpCode;
        public readonly byte[] Payload;

        public WireInputItem(uint playerId, int opCode, byte[] payload)
        {
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }

    public readonly struct WireSubmitFrameInputReq
    {
        public readonly ulong RoomId;
        public readonly ulong WorldId;
        public readonly uint PlayerId;
        public readonly int Frame;
        public readonly int InputOpCode;
        public readonly byte[] InputPayload;

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

    public readonly struct WireSubmitFrameInputRes
    {
        public readonly bool Accepted;
        public readonly int ServerFrame;
        public readonly int ReasonCode;

        public WireSubmitFrameInputRes(bool accepted, int serverFrame, int reasonCode)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            ReasonCode = reasonCode;
        }
    }

    public readonly struct WireFramePushedPush
    {
        public readonly ulong RoomId;
        public readonly ulong WorldId;
        public readonly int Frame;
        public readonly WireInputItem[] Inputs;

        public WireFramePushedPush(ulong roomId, ulong worldId, int frame, WireInputItem[] inputs)
        {
            RoomId = roomId;
            WorldId = worldId;
            Frame = frame;
            Inputs = inputs;
        }
    }
}
