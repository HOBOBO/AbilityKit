using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class NetworkSessionBufferOwnershipTests
    {
        [Test]
        public void Receive_WithDeferredIoDispatcher_OwnsTransportBytesBeforeCallbackReturns()
        {
            var transport = new TestTransport();
            var ioDispatcher = new ManualDispatcher();
            using var session = new NetworkSession(
                transport,
                InlineDispatcher.Instance,
                ioDispatcher,
                LengthPrefixedFrameCodec.Instance);
            var expected = CreatePayload(512, 17);
            byte[] received = null;
            session.ServerPushReceived += (_, payload) => received = Copy(payload);
            session.Start();

            var frame = EncodePush(1001, expected);
            transport.Receive(frame);
            Array.Fill(frame.Array, (byte)0xCC, frame.Offset, frame.Count);

            ioDispatcher.RunAll();

            Assert.That(received, Is.EqualTo(expected));
        }

        [Test]
        public void Receive_WithDeferredCallbackDispatcher_KeepsDecodedPayloadStableAcrossLaterFrames()
        {
            var transport = new TestTransport();
            var callbackDispatcher = new ManualDispatcher();
            using var session = new NetworkSession(
                transport,
                callbackDispatcher,
                InlineDispatcher.Instance,
                LengthPrefixedFrameCodec.Instance);
            var firstExpected = CreatePayload(384, 29);
            var secondExpected = CreatePayload(448, 71);
            var received = new List<byte[]>();
            session.ServerPushReceived += (_, payload) => received.Add(Copy(payload));
            session.Start();

            transport.Receive(EncodePush(1001, firstExpected));
            transport.Receive(EncodePush(1002, secondExpected));
            callbackDispatcher.RunAll();

            Assert.That(received, Has.Count.EqualTo(2));
            Assert.That(received[0], Is.EqualTo(firstExpected));
            Assert.That(received[1], Is.EqualTo(secondExpected));
        }

        private static ArraySegment<byte> EncodePush(uint opCode, byte[] payload)
        {
            var header = new NetworkPacketHeader(
                NetworkPacketFlags.ServerPush,
                opCode,
                0,
                (uint)payload.Length);
            return LengthPrefixedFrameCodec.Instance.Encode(
                header,
                new ArraySegment<byte>(payload));
        }

        private static byte[] CreatePayload(int length, int seed)
        {
            var payload = new byte[length];
            for (var i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)((seed + i * 31) & 0xFF);
            }

            return payload;
        }

        private static byte[] Copy(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var copy = new byte[bytes.Count];
            Buffer.BlockCopy(bytes.Array, bytes.Offset, copy, 0, bytes.Count);
            return copy;
        }

        private sealed class ManualDispatcher : IDispatcher
        {
            private readonly Queue<Action> _pending = new Queue<Action>();

            public void Post(Action action)
            {
                _pending.Enqueue(action ?? throw new ArgumentNullException(nameof(action)));
            }

            public void RunAll()
            {
                while (_pending.Count > 0)
                {
                    _pending.Dequeue().Invoke();
                }
            }
        }

        private sealed class TestTransport : ITransport
        {
            public bool IsConnected { get; private set; }

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<ArraySegment<byte>> BytesReceived;

            public void Connect(string host, int port)
            {
                IsConnected = true;
                Connected?.Invoke();
            }

            public void Close()
            {
                if (!IsConnected)
                {
                    return;
                }

                IsConnected = false;
                Disconnected?.Invoke();
            }

            public void Send(ArraySegment<byte> bytes)
            {
            }

            public void Receive(ArraySegment<byte> bytes)
            {
                BytesReceived?.Invoke(bytes);
            }

            public void Dispose()
            {
                Close();
            }
        }
    }
}
