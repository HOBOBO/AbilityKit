using System;
using System.Buffers.Binary;

namespace AbilityKit.Network.Protocol
{
    public readonly struct NetworkPacketHeader
    {
        public const int Size = 16;

        public readonly NetworkPacketFlags Flags;
        public readonly ushort HeaderSize;
        public readonly uint OpCode;
        public readonly uint Seq;
        public readonly uint PayloadLength;

        public NetworkPacketHeader(NetworkPacketFlags flags, uint opCode, uint seq, uint payloadLength)
        {
            Flags = flags;
            HeaderSize = Size;
            OpCode = opCode;
            Seq = seq;
            PayloadLength = payloadLength;
        }

        public static NetworkPacketHeader Read(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length < Size) throw new ArgumentException("Insufficient header length.", nameof(headerBytes));

            var flags = (NetworkPacketFlags)BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.Slice(0, 2));
            var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(headerBytes.Slice(2, 2));
            var opCode = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(4, 4));
            var seq = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(8, 4));
            var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.Slice(12, 4));

            if (headerSize != Size) throw new InvalidOperationException($"Unsupported header size: {headerSize}.");

            return new NetworkPacketHeader(flags, opCode, seq, payloadLength);
        }

        public void Write(Span<byte> headerBytes)
        {
            if (headerBytes.Length < Size) throw new ArgumentException("Insufficient header length.", nameof(headerBytes));

            BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(0, 2), (ushort)Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(2, 2), HeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(4, 4), OpCode);
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(8, 4), Seq);
            BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(12, 4), PayloadLength);
        }
    }
}
