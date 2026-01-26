using System.Buffers.Binary;

namespace AbilityKit.Orleans.Gateway.TcpGateway;

public enum TcpGatewayStatusCode : int
{
    Ok = 0,
    UnhandledOpCode = 1,
    Timeout = 2,
    Exception = 3,
    BadRequest = 4
}

public readonly struct TcpGatewayResponseEnvelope
{
    public readonly TcpGatewayStatusCode StatusCode;
    public readonly ReadOnlyMemory<byte> Payload;

    public TcpGatewayResponseEnvelope(TcpGatewayStatusCode statusCode, ReadOnlyMemory<byte> payload)
    {
        StatusCode = statusCode;
        Payload = payload;
    }
}

public static class TcpGatewayResponseCodec
{
    public static byte[] Serialize(TcpGatewayResponseEnvelope envelope)
    {
        var payloadLen = envelope.Payload.Length;
        var bytes = new byte[4 + payloadLen];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), (int)envelope.StatusCode);
        if (payloadLen > 0)
        {
            envelope.Payload.Span.CopyTo(bytes.AsSpan(4));
        }

        return bytes;
    }
}
