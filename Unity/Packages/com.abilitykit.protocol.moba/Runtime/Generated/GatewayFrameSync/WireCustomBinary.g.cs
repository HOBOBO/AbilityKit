using System;
using System.IO;
using System.Buffers.Binary;
using System.Text;

namespace AbilityKit.Protocol.Moba.Generated.GatewayFrameSync
{
    public static class WireCustomBinary
    {
        public static ArraySegment<byte> Serialize(in WireSubmitFrameInputReq req)
        {
            var bytes = new byte[EstimateSize(in req)];
            var written = Write(in req, bytes);
            if (written == bytes.Length) return new ArraySegment<byte>(bytes);

            var trimmed = new byte[written];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, written);
            return new ArraySegment<byte>(trimmed);
        }

        public static ArraySegment<byte> Serialize(in WireSubmitFrameInputRes res)
        {
            var bytes = new byte[1 + 4 + 4];
            var written = Write(in res, bytes);
            if (written == bytes.Length) return new ArraySegment<byte>(bytes);

            var trimmed = new byte[written];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, written);
            return new ArraySegment<byte>(trimmed);
        }

        public static ArraySegment<byte> Serialize(in WireFramePushedPush push)
        {
            var bytes = new byte[EstimateSize(in push)];
            var written = Write(in push, bytes);
            if (written == bytes.Length) return new ArraySegment<byte>(bytes);

            var trimmed = new byte[written];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, written);
            return new ArraySegment<byte>(trimmed);
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ArraySegment<byte> payload)
        {
            return DeserializeSubmitFrameInputReq(payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ReadOnlyMemory<byte> payload)
        {
            return DeserializeSubmitFrameInputReq(payload.Span);
        }

        public static WireSubmitFrameInputReq DeserializeSubmitFrameInputReq(ReadOnlySpan<byte> payload)
        {
            return ReadSubmitFrameInputReq(payload);
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ArraySegment<byte> payload)
        {
            return DeserializeSubmitFrameInputRes(payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ReadOnlyMemory<byte> payload)
        {
            return DeserializeSubmitFrameInputRes(payload.Span);
        }

        public static WireSubmitFrameInputRes DeserializeSubmitFrameInputRes(ReadOnlySpan<byte> payload)
        {
            return ReadSubmitFrameInputRes(payload);
        }

        public static WireFramePushedPush DeserializeFramePushedPush(ArraySegment<byte> payload)
        {
            return DeserializeFramePushedPush(payload.Array == null
                ? ReadOnlySpan<byte>.Empty
                : new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count));
        }

        public static WireFramePushedPush DeserializeFramePushedPush(ReadOnlyMemory<byte> payload)
        {
            return DeserializeFramePushedPush(payload.Span);
        }

        public static WireFramePushedPush DeserializeFramePushedPush(ReadOnlySpan<byte> payload)
        {
            return ReadFramePushedPush(payload);
        }

        private static int EstimateSize(in WireSubmitFrameInputReq req)
        {
            var payloadLen = req.InputPayload != null ? req.InputPayload.Length : 0;
            return 8 + 8 + 4 + 4 + 4 + 4 + payloadLen;
        }

        private static int EstimateSize(in WireFramePushedPush push)
        {
            var inputs = push.Inputs;
            var size = 8 + 8 + 4 + 4;
            if (inputs == null || inputs.Length == 0) return size;

            for (int i = 0; i < inputs.Length; i++)
            {
                var p = inputs[i].Payload;
                var payloadLen = p != null ? p.Length : 0;
                size += 4 + 4 + 4 + payloadLen;
            }
            return size;
        }

        private static int Write(in WireSubmitFrameInputReq req, byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(req.RoomId);
            bw.Write(req.WorldId);
            bw.Write(req.PlayerId);
            bw.Write(req.Frame);
            bw.Write(req.InputOpCode);

            var payload = req.InputPayload;
            if (payload == null || payload.Length == 0)
            {
                bw.Write(0);
            }
            else
            {
                bw.Write(payload.Length);
                bw.Write(payload);
            }

            bw.Flush();
            return (int)ms.Position;
        }

        private static int Write(in WireSubmitFrameInputRes res, byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(res.Accepted);
            bw.Write(res.ServerFrame);
            bw.Write(res.ReasonCode);

            bw.Flush();
            return (int)ms.Position;
        }

        private static int Write(in WireFramePushedPush push, byte[] buffer)
        {
            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            bw.Write(push.RoomId);
            bw.Write(push.WorldId);
            bw.Write(push.Frame);

            var inputs = push.Inputs;
            if (inputs == null || inputs.Length == 0)
            {
                bw.Write(0);
                bw.Flush();
                return (int)ms.Position;
            }

            bw.Write(inputs.Length);
            for (int i = 0; i < inputs.Length; i++)
            {
                var it = inputs[i];
                bw.Write(it.PlayerId);
                bw.Write(it.OpCode);

                var payload = it.Payload;
                if (payload == null || payload.Length == 0)
                {
                    bw.Write(0);
                }
                else
                {
                    bw.Write(payload.Length);
                    bw.Write(payload);
                }
            }

            bw.Flush();
            return (int)ms.Position;
        }

        private static WireSubmitFrameInputReq ReadSubmitFrameInputReq(ReadOnlySpan<byte> bytes)
        {
            var r = new SpanReader(bytes);
            var roomId = r.ReadUInt64();
            var worldId = r.ReadUInt64();
            var playerId = r.ReadUInt32();
            var frame = r.ReadInt32();
            var inputOpCode = r.ReadInt32();
            var payload = r.ReadBytesWithLength();
            return new WireSubmitFrameInputReq(roomId, worldId, playerId, frame, inputOpCode, payload);
        }

        private static WireSubmitFrameInputRes ReadSubmitFrameInputRes(ReadOnlySpan<byte> bytes)
        {
            var r = new SpanReader(bytes);
            var accepted = r.ReadBoolean();
            var serverFrame = r.ReadInt32();
            var reasonCode = r.ReadInt32();
            return new WireSubmitFrameInputRes(accepted, serverFrame, reasonCode);
        }

        private static WireFramePushedPush ReadFramePushedPush(ReadOnlySpan<byte> bytes)
        {
            var r = new SpanReader(bytes);
            var roomId = r.ReadUInt64();
            var worldId = r.ReadUInt64();
            var frame = r.ReadInt32();

            var count = r.ReadInt32();
            if (count <= 0) return new WireFramePushedPush(roomId, worldId, frame, Array.Empty<WireInputItem>());

            var inputs = new WireInputItem[count];
            for (int i = 0; i < count; i++)
            {
                var playerId = r.ReadUInt32();
                var opCode = r.ReadInt32();
                var payload = r.ReadBytesWithLength();
                inputs[i] = new WireInputItem(playerId, opCode, payload);
            }

            return new WireFramePushedPush(roomId, worldId, frame, inputs);
        }

        private ref struct SpanReader
        {
            private ReadOnlySpan<byte> _span;
            private int _offset;

            public SpanReader(ReadOnlySpan<byte> span)
            {
                _span = span;
                _offset = 0;
            }

            public bool ReadBoolean()
            {
                if (_offset + 1 > _span.Length) throw new InvalidOperationException("EOF");
                var v = _span[_offset] != 0;
                _offset += 1;
                return v;
            }

            public int ReadInt32()
            {
                if (_offset + 4 > _span.Length) throw new InvalidOperationException("EOF");
                var v = BinaryPrimitives.ReadInt32LittleEndian(_span.Slice(_offset, 4));
                _offset += 4;
                return v;
            }

            public uint ReadUInt32()
            {
                if (_offset + 4 > _span.Length) throw new InvalidOperationException("EOF");
                var v = BinaryPrimitives.ReadUInt32LittleEndian(_span.Slice(_offset, 4));
                _offset += 4;
                return v;
            }

            public ulong ReadUInt64()
            {
                if (_offset + 8 > _span.Length) throw new InvalidOperationException("EOF");
                var v = BinaryPrimitives.ReadUInt64LittleEndian(_span.Slice(_offset, 8));
                _offset += 8;
                return v;
            }

            public byte[] ReadBytesWithLength()
            {
                var len = ReadInt32();
                if (len <= 0) return Array.Empty<byte>();
                if (_offset + len > _span.Length) throw new InvalidOperationException("EOF");
                var bytes = new byte[len];
                _span.Slice(_offset, len).CopyTo(bytes);
                _offset += len;
                return bytes;
            }
        }
    }
}
