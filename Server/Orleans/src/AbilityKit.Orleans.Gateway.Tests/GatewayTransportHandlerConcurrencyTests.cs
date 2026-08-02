using System.Collections.Concurrent;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AbilityKit.Orleans.Gateway.Tests;

public sealed class GatewayTransportHandlerConcurrencyTests
{
    [Fact]
    public async Task OnRequest_should_not_block_another_connection_behind_a_slow_request()
    {
        var registry = new GatewaySessionRegistry();
        var backgroundTasks = new GatewayBackgroundTaskQueue(
            NullLogger<GatewayBackgroundTaskQueue>.Instance);
        var router = new BlockingFirstRequestRouter();
        var frameSyncSubscriptions = new GatewayFrameSyncSubscriptionManager(
            clusterClient: null!,
            registry,
            backgroundTasks,
            NullLogger<GatewayFrameSyncSubscriptionManager>.Instance);
        var stateSyncPushSubscriptions = new GatewayStateSyncPushSubscriptionManager(
            clusterClient: null!,
            registry,
            backgroundTasks,
            NullLogger<GatewayStateSyncPushSubscriptionManager>.Instance);
        var handler = new GatewayTransportHandler(
            registry,
            router,
            roomMembership: null!,
            backgroundTasks,
            frameSyncSubscriptions,
            stateSyncPushSubscriptions,
            NullLogger<GatewayTransportHandler>.Instance);
        var slowSession = new RecordingTransportSession(1);
        var fastSession = new RecordingTransportSession(2);
        handler.RegisterSession(slowSession);
        handler.RegisterSession(fastSession);

        handler.OnRequest(slowSession.ConnectionId, opCode: 108, seq: 1, payload: [1]);
        await router.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        handler.OnRequest(fastSession.ConnectionId, opCode: 108, seq: 2, payload: [2]);
        var fastResponse = await fastSession.ResponseSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal((uint)2, fastResponse.Seq);
        Assert.False(slowSession.ResponseSent.Task.IsCompleted);

        router.ReleaseFirstRequest.TrySetResult();
        var slowResponse = await slowSession.ResponseSent.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal((uint)1, slowResponse.Seq);
    }

    [Fact]
    public async Task OnRequest_should_preserve_request_order_within_a_connection()
    {
        var registry = new GatewaySessionRegistry();
        var backgroundTasks = new GatewayBackgroundTaskQueue(
            NullLogger<GatewayBackgroundTaskQueue>.Instance);
        var router = new BlockingFirstRequestRouter();
        var frameSyncSubscriptions = new GatewayFrameSyncSubscriptionManager(
            clusterClient: null!,
            registry,
            backgroundTasks,
            NullLogger<GatewayFrameSyncSubscriptionManager>.Instance);
        var stateSyncPushSubscriptions = new GatewayStateSyncPushSubscriptionManager(
            clusterClient: null!,
            registry,
            backgroundTasks,
            NullLogger<GatewayStateSyncPushSubscriptionManager>.Instance);
        var handler = new GatewayTransportHandler(
            registry,
            router,
            roomMembership: null!,
            backgroundTasks,
            frameSyncSubscriptions,
            stateSyncPushSubscriptions,
            NullLogger<GatewayTransportHandler>.Instance);
        var session = new RecordingTransportSession(1, expectedResponseCount: 2);
        handler.RegisterSession(session);

        handler.OnRequest(session.ConnectionId, opCode: 108, seq: 1, payload: [1]);
        await router.FirstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        handler.OnRequest(session.ConnectionId, opCode: 108, seq: 2, payload: [2]);
        await Task.Delay(100);
        Assert.False(router.SecondRequestStarted.Task.IsCompleted);

        router.ReleaseFirstRequest.TrySetResult();
        await session.AllResponsesSent.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal([1u, 2u], session.ResponseSequences.ToArray());
    }

    private sealed class BlockingFirstRequestRouter : IGatewayRequestRouter
    {
        public TaskCompletionSource FirstRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRequest { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondRequestStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GatewayResponse> RouteAsync(
            GatewaySessionContext context,
            uint opCode,
            uint seq,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            if (seq == 1)
            {
                FirstRequestStarted.TrySetResult();
                await ReleaseFirstRequest.Task.WaitAsync(cancellationToken);
            }

            else if (seq == 2)
            {
                SecondRequestStarted.TrySetResult();
            }

            return GatewayResponse.Ok(seq);
        }
    }

    private sealed class RecordingTransportSession : IGatewayTransportSession
    {
        private readonly int _expectedResponseCount;
        private int _responseCount;

        public RecordingTransportSession(
            long connectionId,
            int expectedResponseCount = 1)
        {
            ConnectionId = connectionId;
            Context = new GatewaySessionContext(connectionId);
            _expectedResponseCount = expectedResponseCount;
        }

        public long ConnectionId { get; }

        public string TransportName => "TestTransport";

        public GatewaySessionContext Context { get; }

        public bool IsConnected => true;

        public TaskCompletionSource<ResponseRecord> ResponseSent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllResponsesSent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<uint> ResponseSequences { get; } = new();

        public Task SendResponseAsync(
            uint opCode,
            uint seq,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            ResponseSequences.Enqueue(seq);
            ResponseSent.TrySetResult(new ResponseRecord(opCode, seq, payload));
            if (Interlocked.Increment(ref _responseCount) == _expectedResponseCount)
            {
                AllResponsesSent.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public Task SendServerPushAsync(
            uint opCode,
            byte[] payload,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record ResponseRecord(uint OpCode, uint Seq, byte[] Payload);
}
