using System.Collections.Concurrent;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayRoomClientBoundaryTests
{
    [Fact]
    public async Task Transport_ForwardsExactRequestArguments()
    {
        const uint opCode = 9123U;
        var payload = new ArraySegment<byte>(new byte[] { 1, 2, 3 });
        var timeout = TimeSpan.FromSeconds(7);
        using var cancellation = new CancellationTokenSource();
        uint actualOpCode = 0;
        ArraySegment<byte> actualPayload = default;
        TimeSpan? actualTimeout = null;
        CancellationToken actualToken = default;
        using var transport = NewTransport(
            (code, bytes, duration, token) =>
            {
                actualOpCode = code;
                actualPayload = bytes;
                actualTimeout = duration;
                actualToken = token;
                return Task.FromResult(default(ArraySegment<byte>));
            });

        await transport.SendRequestAsync(
            opCode,
            payload,
            timeout,
            cancellation.Token);

        Assert.Equal(opCode, actualOpCode);
        Assert.Equal(payload, actualPayload);
        Assert.Equal(timeout, actualTimeout);
        Assert.Equal(cancellation.Token, actualToken);
    }

    [Fact]
    public void Transport_PairsPushSubscriptionAndDisposesOwnedDependencyOnce()
    {
        Action<uint, ArraySegment<byte>>? subscribed = null;
        Action<uint, ArraySegment<byte>>? unsubscribed = null;
        var owned = new CountingDisposable();
        var transport = new GatewayRoomTransportAdapter(
            (_, _, _, _) => Task.FromResult(default(ArraySegment<byte>)),
            handler => subscribed = handler,
            handler => unsubscribed = handler,
            owned);
        static void Handler(uint _, ArraySegment<byte> __) { }

        transport.ServerPushReceived += Handler;
        transport.ServerPushReceived -= Handler;
        transport.Dispose();
        transport.Dispose();

        Assert.Same((Action<uint, ArraySegment<byte>>)Handler, subscribed);
        Assert.Same((Action<uint, ArraySegment<byte>>)Handler, unsubscribed);
        Assert.Equal(1, owned.DisposeCount);
    }

    [Fact]
    public void Sequence_StartsAtOneAndIsUniqueUnderConcurrency()
    {
        const int count = 512;
        var sequence = new BattleInputCommandSequence();
        var values = new ConcurrentBag<ulong>();

        Parallel.For(0, count, _ => values.Add(sequence.Next()));

        Assert.Equal(count, values.Distinct().Count());
        Assert.Equal(1UL, values.Min());
        Assert.Equal((ulong)count, values.Max());
    }

    [Fact]
    public async Task WireClient_PreservesBattleInputProtocolAndResponseFields()
    {
        const uint submitOpCode = 8107U;
        WireSubmitBattleInputReq captured = default;
        using var transport = NewTransport((opCode, payload, _, _) =>
        {
            Assert.Equal(submitOpCode, opCode);
            captured = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(
                payload);
            var response = new WireSubmitBattleInputRes
            {
                Success = false,
                AcceptedFrame = 21,
                CurrentFrame = 24,
                Status = "late",
                Message = "resync",
                ShouldResync = true,
                ServerTicks = 987654L
            };
            return Task.FromResult(WireRoomGatewayBinary.Serialize(in response));
        });
        var wireClient = new GatewayRoomWireProtocolClient(
            transport,
            CreateOpCodes(submitOpCode),
            new BattleInputCommandSequence());

        var result = await wireClient.SubmitBattleInputAsync(
            "session-1",
            "battle-1",
            42UL,
            20,
            7U,
            901,
            new byte[] { 4, 5, 6 },
            TimeSpan.FromSeconds(1));

        Assert.Equal("session-1", captured.SessionToken);
        Assert.Equal("battle-1", captured.BattleId);
        Assert.Equal(42UL, captured.WorldId);
        Assert.Equal(20, captured.Frame);
        Assert.Equal(7U, captured.PlayerId);
        Assert.Equal(901, captured.InputOpCode);
        Assert.Equal(new byte[] { 4, 5, 6 }, captured.Payload);
        Assert.Equal(1UL, captured.CommandSequence);
        Assert.False(result.Success);
        Assert.Equal(21, result.AcceptedFrame);
        Assert.Equal(24, result.CurrentFrame);
        Assert.Equal("late", result.Status);
        Assert.Equal("resync", result.Message);
        Assert.True(result.ShouldResync);
        Assert.Equal(987654L, result.ServerTicks);
        Assert.Equal(1UL, result.CommandSequence);
    }

    private static GatewayRoomTransportAdapter NewTransport(
        Func<uint, ArraySegment<byte>, TimeSpan?, CancellationToken,
            Task<ArraySegment<byte>>> send)
    {
        return new GatewayRoomTransportAdapter(send, _ => { }, _ => { });
    }

    private static GatewayRoomOpCodes CreateOpCodes(uint submitBattleInput)
    {
        return new GatewayRoomOpCodes(
            createRoom: 8101U,
            joinRoom: 8102U,
            subscribeStateSync: 8103U,
            setReady: 8104U,
            pickHero: 8105U,
            startBattle: 8106U,
            submitBattleInput: submitBattleInput,
            snapshotPushed: 8201U,
            deltaSnapshotPushed: 8202U);
    }

    private sealed class CountingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
