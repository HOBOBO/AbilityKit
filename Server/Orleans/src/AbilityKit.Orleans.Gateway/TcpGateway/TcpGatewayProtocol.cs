using System.Buffers.Binary;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

[System.Flags]
public enum NetworkPacketFlags : ushort
{
    None = 0,
    Compressed = 1 << 0,
    Encrypted = 1 << 1,
    Heartbeat = 1 << 2,
    Request = 1 << 3,
    Response = 1 << 4,
    ServerPush = 1 << 5
}

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
        BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(0, 2), (ushort)Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(headerBytes.Slice(2, 2), HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(4, 4), OpCode);
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(8, 4), Seq);
        BinaryPrimitives.WriteUInt32LittleEndian(headerBytes.Slice(12, 4), PayloadLength);
    }
}

public static class NetworkFrameCodec
{
    public static int GetFrameSize(int payloadLength) => 4 + NetworkPacketHeader.Size + payloadLength;

    public static void WriteFrame(Span<byte> destination, NetworkPacketHeader header, ReadOnlySpan<byte> payload)
    {
        if (payload.Length != header.PayloadLength) throw new ArgumentException("Payload length mismatch.", nameof(payload));

        var frameLength = NetworkPacketHeader.Size + payload.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), (uint)frameLength);
        header.Write(destination.Slice(4, NetworkPacketHeader.Size));
        payload.CopyTo(destination.Slice(4 + NetworkPacketHeader.Size, payload.Length));
    }

    public static bool TryParseFrame(ReadOnlySpan<byte> source, out int totalSize, out NetworkPacketHeader header, out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;
        totalSize = 0;

        if (source.Length < 4 + NetworkPacketHeader.Size) return false;

        var frameLength = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(0, 4));
        if (frameLength < NetworkPacketHeader.Size) throw new InvalidOperationException("Invalid frame length.");

        totalSize = 4 + (int)frameLength;
        if (source.Length < totalSize) return false;

        header = NetworkPacketHeader.Read(source.Slice(4, NetworkPacketHeader.Size));
        if (header.PayloadLength != frameLength - NetworkPacketHeader.Size) throw new InvalidOperationException("Payload length mismatch.");

        payload = source.Slice(4 + NetworkPacketHeader.Size, (int)header.PayloadLength);
        return true;
    }
}
