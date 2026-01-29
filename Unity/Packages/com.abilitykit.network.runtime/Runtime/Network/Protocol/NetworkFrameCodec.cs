using System;
using System.Buffers.Binary;

namespace AbilityKit.Network.Protocol
{
    public static class NetworkFrameCodec
    {
        public static int GetFrameSize(int payloadLength)
        {
            if (payloadLength < 0) throw new ArgumentOutOfRangeException(nameof(payloadLength));
            checked
            {
                return 4 + NetworkPacketHeader.Size + payloadLength;
            }
        }

        public static void WriteFrame(Span<byte> destination, NetworkPacketHeader header, ReadOnlySpan<byte> payload)
        {
            if (payload.Length != header.PayloadLength) throw new ArgumentException("Payload length mismatch.", nameof(payload));
            var frameLength = NetworkPacketHeader.Size + payload.Length;
            if (destination.Length < 4 + frameLength) throw new ArgumentException("Destination too small.", nameof(destination));

            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), (uint)frameLength);
            header.Write(destination.Slice(4, NetworkPacketHeader.Size));
            payload.CopyTo(destination.Slice(4 + NetworkPacketHeader.Size, payload.Length));
        }

        public static bool TryReadLengthPrefix(ReadOnlySpan<byte> source, out uint frameLength)
        {
            if (source.Length < 4)
            {
                frameLength = 0;
                return false;
            }

            frameLength = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(0, 4));
            return true;
        }

        public static bool TryParseFrame(ReadOnlySpan<byte> source, out NetworkPacketHeader header, out ReadOnlySpan<byte> payload)
        {
            header = default;
            payload = default;

            if (source.Length < 4 + NetworkPacketHeader.Size) return false;

            var frameLength = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(0, 4));
            if (frameLength < NetworkPacketHeader.Size) return false;

            var totalSize = 4 + (int)frameLength;
            if (source.Length < totalSize) return false;

            header = NetworkPacketHeader.Read(source.Slice(4, NetworkPacketHeader.Size));
            if (header.PayloadLength != frameLength - NetworkPacketHeader.Size) return false;

            payload = source.Slice(4 + NetworkPacketHeader.Size, (int)header.PayloadLength);
            return true;
        }
    }
}
