using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace AbilityKit.Protocol.Moba.GatewayTimeSync
{
    public static class WireTimeSyncBinary
    {
        public static ArraySegment<byte> Serialize(in WireTimeSyncReq req)
        {
            var bytes = new byte[8];
            var written = Write(in req, bytes);
            if (written == bytes.Length) return new ArraySegment<byte>(bytes);

            var trimmed = new byte[written];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, written);
            return new ArraySegment<byte>(trimmed);
        }

        public static ArraySegment<byte> Serialize(in WireTimeSyncRes res)
        {
            var bytes = new byte[8 + 8 + 8];
            var written = Write(in res, bytes);
            if (written == bytes.Length) return new ArraySegment<byte>(bytes);

            var trimmed = new byte[written];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, written);
            return new ArraySegment<byte>(trimmed);
        }

        public static WireTimeSyncReq DeserializeTimeSyncReq(ArraySegment<byte> payload)
        {
            return DeserializeTimeSyncReq(payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public static WireTimeSyncReq DeserializeTimeSyncReq(ReadOnlySpan<byte> payload)
        {
            return ReadTimeSyncReq(payload);
        }

        public static WireTimeSyncRes DeserializeTimeSyncRes(ArraySegment<byte> payload)
        {
            return DeserializeTimeSyncRes(payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public static WireTimeSyncRes DeserializeTimeSyncRes(ReadOnlySpan<byte> payload)
        {
            return ReadTimeSyncRes(payload);
        }

        private static int Write(in WireTimeSyncReq req, byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            bw.Write(req.ClientSendTicks);
            bw.Flush();
            return (int)ms.Position;
        }

        private static int Write(in WireTimeSyncRes res, byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            bw.Write(res.ClientSendTicks);
            bw.Write(res.ServerNowTicks);
            bw.Write(res.ServerTickFrequency);
            bw.Flush();
            return (int)ms.Position;
        }

        private static WireTimeSyncReq ReadTimeSyncReq(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 8) throw new InvalidOperationException("EOF");
            var clientSendTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0, 8));
            return new WireTimeSyncReq(clientSendTicks);
        }

        private static WireTimeSyncRes ReadTimeSyncRes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 24) throw new InvalidOperationException("EOF");
            var clientSendTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(0, 8));
            var serverNowTicks = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(8, 8));
            var serverFreq = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
            return new WireTimeSyncRes(clientSendTicks, serverNowTicks, serverFreq);
        }
    }
}
