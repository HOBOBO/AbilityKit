using System;
using MemoryPack;

namespace AbilityKit.Protocol.Shooter
{
    [MemoryPackable]
    public partial struct ShooterHostInputRequest
    {
        [MemoryPackOrder(0)] public string WorldId;
        [MemoryPackOrder(1)] public int Frame;
        [MemoryPackOrder(2)] public ShooterPlayerCommand[] Commands;

        [MemoryPackConstructor]
        public ShooterHostInputRequest(string worldId, int frame, ShooterPlayerCommand[] commands)
        {
            WorldId = worldId;
            Frame = frame;
            Commands = commands;
        }
    }

    [MemoryPackable]
    public partial struct ShooterHostInputResponse
    {
        [MemoryPackOrder(0)] public bool Accepted;
        [MemoryPackOrder(1)] public int ServerFrame;
        [MemoryPackOrder(2)] public int AcceptedCommandCount;
        [MemoryPackOrder(3)] public int ReasonCode;

        public ShooterHostInputResponse(
            bool accepted,
            int serverFrame,
            int acceptedCommandCount,
            int reasonCode)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            AcceptedCommandCount = acceptedCommandCount;
            ReasonCode = reasonCode;
        }
    }

    public static class ShooterHostInputCodec
    {
        public static ArraySegment<byte> Serialize(in ShooterHostInputRequest request)
        {
            return new ArraySegment<byte>(MemoryPackSerializer.Serialize(request));
        }

        public static ArraySegment<byte> Serialize(in ShooterHostInputResponse response)
        {
            return new ArraySegment<byte>(MemoryPackSerializer.Serialize(response));
        }

        public static ShooterHostInputRequest DeserializeRequest(ArraySegment<byte> payload)
        {
            return MemoryPackSerializer.Deserialize<ShooterHostInputRequest>(AsSpan(payload));
        }

        public static ShooterHostInputResponse DeserializeResponse(ArraySegment<byte> payload)
        {
            return MemoryPackSerializer.Deserialize<ShooterHostInputResponse>(AsSpan(payload));
        }

        private static ReadOnlySpan<byte> AsSpan(ArraySegment<byte> payload)
        {
            return payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count);
        }
    }
}
