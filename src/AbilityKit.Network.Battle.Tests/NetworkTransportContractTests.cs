using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using Xunit;

namespace AbilityKit.Network.Battle.Tests;

/// <summary>
/// Contract tests for <see cref="NetworkTransport"/>'s data-plane behavior — the seams shooter's
/// P2.2 migration depends on: fire-and-forget vs awaitable input submit (with authoritative-frame
/// retry), push dispatch routing, and the raw-before-typed decoding order. A fake
/// <see cref="IConnection"/> is injected via <see cref="NetworkTransportOptions.ConnectionFactory"/>
/// so tests run at the parsed-packet level without real sockets or byte framing.
/// </summary>
public sealed class NetworkTransportContractTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TestDeadline = WaitLimit + TimeSpan.FromSeconds(1);

    private const uint OpSubmitInput = 5101u;
    private const uint OpSubscribe = 5102u;
    private const uint OpFramePushed = 9001u;
    private const uint OpSnapshotPushed = 5202u;
    private const uint OpDeltaSnapshotPushed = 5212u;
    private const uint OpReliableEventsPushed = 5203u;

    // ---------- input uplink: fire-and-forget ----------

    [Fact]
    public void SendInput_WithoutResponseDeserializer_IsFireAndForgetAndSendsSubmitInputPacket()
    {
        var conn = new FakeBattleConnection();
        var serializeCalls = 0;
        using var transport = NewTransport(conn, o =>
        {
            o.OpSubmitInput = OpSubmitInput;
            o.SerializeSubmitInput = _ =>
            {
                serializeCalls++;
                return new ArraySegment<byte>(new byte[] { 0xAB });
            };
            // DeserializeSubmitInputResponse intentionally null => fire-and-forget path
        });

        Assert.True(transport.IsAuthenticated);
        transport.SendInput(NewInput(frame: 7));

        var sent = Assert.Single(conn.Sent);
        Assert.Equal(OpSubmitInput, sent.OpCode);
        Assert.Equal(1, serializeCalls);
        Assert.Equal(0xAB, sent.Payload.Array![sent.Payload.Offset]);
    }

    // ---------- input uplink: awaitable + authoritative-frame retry ----------

    [Fact]
    public async Task SendInputAsync_WaitsForSubscriptionResponseBeforeSending()
    {
        var conn = new FakeBattleConnection { AutoRespondOpCode = OpSubmitInput };
        conn.AutoResponses.Enqueue(new byte[] { 0 });
        using var transport = NewTransport(conn, ConfigureSubscriptionAndSubmit);

        var subscription = Assert.Single(conn.Sent);
        Assert.Equal(OpSubscribe, subscription.OpCode);
        Assert.NotEqual(0u, subscription.Seq);
        Assert.False(transport.IsAuthenticated);

        var pending = transport.SendInputAsync(NewInput(frame: 1), WaitLimit);
        Assert.False(pending.IsCompleted);
        Assert.Single(conn.Sent);

        conn.CompleteResponse(subscription.OpCode, subscription.Seq, new byte[] { 7 });
        var result = await pending.WaitAsync(TestDeadline);

        Assert.True(result.Accepted);
        Assert.True(transport.IsAuthenticated);
        Assert.Collection(conn.Sent,
            sent => Assert.Equal(OpSubscribe, sent.OpCode),
            sent => Assert.Equal(OpSubmitInput, sent.OpCode));
    }

    [Fact]
    public async Task SendInputAsync_CancelledDuringAuthentication_DoesNotSendInput()
    {
        var conn = new FakeBattleConnection();
        using var transport = NewTransport(conn, ConfigureSubscriptionAndSubmit);
        using var cancellation = new System.Threading.CancellationTokenSource();
        var pending = transport.SendInputAsync(NewInput(frame: 1), WaitLimit, cancellation.Token);
        Assert.False(pending.IsCompleted);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TestDeadline));
        Assert.Equal(OpSubscribe, Assert.Single(conn.Sent).OpCode);
    }

    [Fact]
    public async Task SendInputAsync_AcceptedResponse_ReturnsResultAndAcksServerFrame()
    {
        var conn = new FakeBattleConnection { AutoRespondOpCode = OpSubmitInput };
        conn.AutoResponses.Enqueue(new byte[] { 0 }); // body code 0 => accepted, ServerFrame 99
        var ackedFrame = -1;
        using var transport = NewTransport(conn, o =>
        {
            o.OpSubmitInput = OpSubmitInput;
            o.SerializeSubmitInput = _ => new ArraySegment<byte>(new byte[] { 1 });
            o.DeserializeSubmitInputResponse = DecodeResponse;
            o.OnSubmitInputAck = f => ackedFrame = f;
        });

        var result = await transport.SendInputAsync(NewInput(frame: 1), WaitLimit).WaitAsync(TestDeadline);

        Assert.True(result.Accepted);
        Assert.Equal(99, result.ServerFrame);
        Assert.Equal(42, result.AcceptedFrame);
        Assert.Equal(123456789L, result.ServerTicks);
        Assert.False(result.ShouldResync);
        Assert.Equal(99, ackedFrame);
        Assert.Single(conn.Sent); // single attempt, no retry
    }

    [Fact]
    public async Task SendInputAsync_RetryAtAuthoritativeFrame_RewritesAndRetriesOnce()
    {
        var conn = new FakeBattleConnection { AutoRespondOpCode = OpSubmitInput };
        conn.AutoResponses.Enqueue(new byte[] { 1 }); // retry: ServerFrame 10, RetryAtAuthoritativeFrame
        conn.AutoResponses.Enqueue(new byte[] { 0 }); // then accepted
        var rewrittenFrame = -1;
        using var transport = NewTransport(conn, o =>
        {
            o.OpSubmitInput = OpSubmitInput;
            o.SubmitInputRetryFrameLead = 2;
            o.SerializeSubmitInput = _ => new ArraySegment<byte>(new byte[] { 1 });
            o.DeserializeSubmitInputResponse = DecodeResponse;
            o.PrepareSubmitInput = req => req;
            o.RewriteSubmitInputFrame = (obj, frame) =>
            {
                rewrittenFrame = frame;
                return obj;
            };
        });

        var result = await transport.SendInputAsync(NewInput(frame: 1), WaitLimit).WaitAsync(TestDeadline);

        Assert.True(result.Accepted);
        Assert.Equal(2, conn.Sent.Count); // attempt 0 + retry
        Assert.Equal(12, rewrittenFrame); // serverFrame(10) + SubmitInputRetryFrameLead(2)
    }

    [Fact]
    public async Task SendInputAsync_RejectedWithoutRetry_DoesNotRetry()
    {
        var conn = new FakeBattleConnection { AutoRespondOpCode = OpSubmitInput };
        conn.AutoResponses.Enqueue(new byte[] { 2 }); // rejected, no retry
        var rewriteCalled = false;
        using var transport = NewTransport(conn, o =>
        {
            o.OpSubmitInput = OpSubmitInput;
            o.SerializeSubmitInput = _ => new ArraySegment<byte>(new byte[] { 1 });
            o.DeserializeSubmitInputResponse = DecodeResponse;
            o.RewriteSubmitInputFrame = (obj, frame) =>
            {
                rewriteCalled = true;
                return obj;
            };
        });

        var result = await transport.SendInputAsync(NewInput(frame: 1), WaitLimit).WaitAsync(TestDeadline);

        Assert.False(result.Accepted);
        Assert.Single(conn.Sent);
        Assert.False(rewriteCalled);
    }

    [Fact]
    public async Task SendInputAsync_WithoutResponseDeserializer_Throws()
    {
        var conn = new FakeBattleConnection();
        using var transport = NewTransport(conn, o =>
        {
            o.OpSubmitInput = OpSubmitInput;
            o.SerializeSubmitInput = _ => default;
            // DeserializeSubmitInputResponse intentionally null
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transport.SendInputAsync(NewInput(frame: 1), WaitLimit).WaitAsync(TestDeadline));
    }

    // ---------- downlink: push dispatch routing ----------

    [Fact]
    public void ServerPush_FrameOpCode_RaisesFramePushedWithDecodedPacket()
    {
        var conn = new FakeBattleConnection();
        FramePacket? received = null;
        var decoderSawRaw = false;
        using var transport = NewTransport(conn, o =>
        {
            o.OpFramePushed = OpFramePushed;
            o.DeserializeFramePushed = payload =>
            {
                decoderSawRaw = payload.Array != null && payload.Count == 2
                    && payload.Array[payload.Offset] == 9;
                return new FramePacket(default, new FrameIndex(5), Array.Empty<PlayerInputCommand>(), null);
            };
        });
        transport.FramePushed += p => received = p;

        conn.RaiseServerPush(OpFramePushed, new ArraySegment<byte>(new byte[] { 9, 9 }));

        Assert.NotNull(received);
        Assert.Equal(5, received!.Frame.Value);
        Assert.True(decoderSawRaw, "frame deserializer must receive the raw payload bytes");
    }

    /// <summary>
    /// Pins the contract shooter relies on: <see cref="NetworkTransport.RawServerPushReceived"/>
    /// fires BEFORE typed decoding, carrying the raw (opCode, payload), so an existing raw apply
    /// pipeline can consume a push without double-processing alongside the typed event.
    /// </summary>
    [Fact]
    public void ServerPush_RawFiresBeforeTypedDecodingWithSameBytes()
    {
        var conn = new FakeBattleConnection();
        var order = new List<string>();
        var rawOpCode = 0u;
        var rawSawBytes = false;
        using var transport = NewTransport(conn, o =>
        {
            o.OpFramePushed = OpFramePushed;
            o.DeserializeFramePushed = _ =>
            {
                order.Add("typed");
                return new FramePacket(default, new FrameIndex(1), Array.Empty<PlayerInputCommand>(), null);
            };
        });
        transport.RawServerPushReceived += (op, payload) =>
        {
            order.Add("raw");
            rawOpCode = op;
            rawSawBytes = payload.Array != null && payload.Count == 3
                && payload.Array[payload.Offset + 2] == 3;
        };

        conn.RaiseServerPush(OpFramePushed, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));

        Assert.Equal(new[] { "raw", "typed" }, order.ToArray());
        Assert.Equal(OpFramePushed, rawOpCode);
        Assert.True(rawSawBytes, "raw handler must receive the un-decoded payload bytes");
    }

    [Fact]
    public void ServerPush_SnapshotAndDeltaOpCodes_BothRaiseStateSyncSnapshotPushed()
    {
        var conn = new FakeBattleConnection();
        var pushCount = 0;
        using var transport = NewTransport(conn, o =>
        {
            o.OpSnapshotPushed = OpSnapshotPushed;
            o.OpDeltaSnapshotPushed = OpDeltaSnapshotPushed;
            o.DeserializeSnapshotPushed = _ => new object();
        });
        transport.StateSyncSnapshotPushed += _ => pushCount++;

        conn.RaiseServerPush(OpSnapshotPushed, new ArraySegment<byte>(new byte[] { 1 }));
        conn.RaiseServerPush(OpDeltaSnapshotPushed, new ArraySegment<byte>(new byte[] { 2 }));

        Assert.Equal(2, pushCount);
    }

    [Fact]
    public void ServerPush_ReliableEventsOpCode_RaisesReliableEventsPushed()
    {
        var conn = new FakeBattleConnection();
        var fired = false;
        using var transport = NewTransport(conn, o =>
        {
            o.OpReliableEventsPushed = OpReliableEventsPushed;
            o.DeserializeReliableEventsPushed = _ => new object();
        });
        transport.ReliableEventsPushed += _ => fired = true;

        conn.RaiseServerPush(OpReliableEventsPushed, new ArraySegment<byte>(new byte[] { 7 }));

        Assert.True(fired);
    }

    [Fact]
    public void ServerPush_UnmatchedOpCode_RaisesRawOnlyAndNoTypedEvent()
    {
        var conn = new FakeBattleConnection();
        var rawCount = 0;
        var frameCount = 0;
        var snapCount = 0;
        var relCount = 0;
        using var transport = NewTransport(conn, o =>
        {
            o.OpFramePushed = OpFramePushed;
            o.OpSnapshotPushed = OpSnapshotPushed;
            o.OpReliableEventsPushed = OpReliableEventsPushed;
            o.DeserializeFramePushed = _ => new FramePacket(default, new FrameIndex(1), Array.Empty<PlayerInputCommand>(), null);
            o.DeserializeSnapshotPushed = _ => new object();
            o.DeserializeReliableEventsPushed = _ => new object();
        });
        transport.RawServerPushReceived += (_, _) => rawCount++;
        transport.FramePushed += _ => frameCount++;
        transport.StateSyncSnapshotPushed += _ => snapCount++;
        transport.ReliableEventsPushed += _ => relCount++;

        conn.RaiseServerPush(9999u, new ArraySegment<byte>(new byte[] { 0 }));

        Assert.Equal(1, rawCount);
        Assert.Equal(0, frameCount);
        Assert.Equal(0, snapCount);
        Assert.Equal(0, relCount);
    }

    // ---------- helpers ----------

    private static NetworkTransport NewTransport(FakeBattleConnection conn, Action<NetworkTransportOptions> configure)
    {
        var options = new NetworkTransportOptions { FrameCodec = LengthPrefixedFrameCodec.Instance };
        configure(options);
        options.ConnectionFactory = () => conn;
        var transport = new NetworkTransport(options);
        transport.Connect();
        return transport;
    }

    private static void ConfigureSubscriptionAndSubmit(NetworkTransportOptions options)
    {
        options.OpPostAuthentication = OpSubscribe;
        options.SerializePostAuthentication = () => new ArraySegment<byte>(new byte[] { 6 });
        options.ValidatePostAuthenticationResponse = payload => Assert.Equal(new byte[] { 7 }, payload.ToArray());
        options.OpSubmitInput = OpSubmitInput;
        options.SerializeSubmitInput = _ => new ArraySegment<byte>(new byte[] { 1 });
        options.DeserializeSubmitInputResponse = DecodeResponse;
    }

    private static SubmitInputRequest NewInput(int frame)
        => new SubmitInputRequest(
            default,
            new PlayerInputCommand(new FrameIndex(frame), default, 100, new byte[] { 1 }));

    /// <summary>Test response body encoding: byte 0 = accepted (ServerFrame 99),
    /// 1 = retry (ServerFrame 10, RetryAtAuthoritativeFrame), else rejected.</summary>
    private static NetworkSubmitInputResponse DecodeResponse(ArraySegment<byte> body)
    {
        var code = (body.Array == null || body.Count == 0) ? (byte)2 : body.Array[body.Offset];
        return code switch
        {
            0 => new NetworkSubmitInputResponse(true, 99, 0, retryAtAuthoritativeFrame: false, "ok",
                acceptedFrame: 42, serverTicks: 123456789L, shouldResync: false),
            1 => new NetworkSubmitInputResponse(false, 10, 7, retryAtAuthoritativeFrame: true, "retry"),
            _ => new NetworkSubmitInputResponse(false, 10, 8, retryAtAuthoritativeFrame: false, "rejected"),
        };
    }

    /// <summary>
    /// Minimal <see cref="IConnection"/> double: captures outbound sends, exposes raisable push/packet
    /// events, and can auto-respond to a request opCode so request/response paths
    /// (<see cref="NetworkTransport.SendInputAsync"/>) can be exercised without a real peer.
    /// </summary>
    private sealed class FakeBattleConnection : IConnection
    {
#pragma warning disable CS0067 // IConnection events not raised by these contract tests
        public bool IsConnected => State == ConnectionState.Connected;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;
        public event Action<string, string>? Kicked;

        public List<SentFrame> Sent { get; } = new();

        public uint AutoRespondOpCode { get; set; }

        public Queue<byte[]> AutoResponses { get; } = new();

        public void Open(string host, int port)
        {
            State = ConnectionState.Connected;
            Connected?.Invoke();
        }

        public void Close()
        {
            State = ConnectionState.Disconnected;
        }

        public void Tick(float deltaTime)
        {
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            Sent.Add(new SentFrame(opCode, flags, seq, payload));
            if (opCode == AutoRespondOpCode && AutoResponses.TryDequeue(out var body))
            {
                CompleteResponse(opCode, seq, body);
            }
        }

        public void CompleteResponse(uint opCode, uint seq, byte[] body)
            => PacketReceived?.Invoke(opCode, seq, new ArraySegment<byte>(body));

        public void RaiseServerPush(uint opCode, ArraySegment<byte> payload)
            => ServerPushReceived?.Invoke(opCode, payload);

        public void Dispose()
        {
        }

        public readonly struct SentFrame
        {
            public SentFrame(uint opCode, ushort flags, uint seq, ArraySegment<byte> payload)
            {
                OpCode = opCode;
                Flags = flags;
                Seq = seq;
                Payload = payload;
            }

            public uint OpCode { get; }
            public ushort Flags { get; }
            public uint Seq { get; }
            public ArraySegment<byte> Payload { get; }
        }
#pragma warning restore CS0067
    }
}
