using System.Collections.Concurrent;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Network.Host.Tests;

public sealed class NetworkHostAdvancedTests
{
    [Fact]
    public void LegacyClockContract_IsAssignableToCoreContract()
    {
        IMonotonicClock legacyClock = StopwatchMonotonicClock.Instance;

        AbilityKit.Core.Timing.IMonotonicClock coreClock = legacyClock;

        Assert.Same(legacyClock, coreClock);
        Assert.True(coreClock.Frequency > 0);
    }

    [Fact]
    public async Task AsyncRequests_AreSerializedPerSessionInArrivalOrder()
    {
        var listener = new InProcessChannelListener();
        var firstStarted = Completion();
        var releaseFirst = Completion();
        var allCompleted = Completion();
        var order = new ConcurrentQueue<string>();
        var handler = new DelegateAsyncHandler(async (_, header, _, token) =>
        {
            order.Enqueue($"start:{header.Seq}");
            if (header.Seq == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(token);
            }
            order.Enqueue($"end:{header.Seq}");
            if (header.Seq == 2) allCompleted.TrySetResult();
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);

        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);

        await Task.Delay(50);
        Assert.Equal(new[] { "start:1" }, order.ToArray());
        releaseFirst.TrySetResult();
        await allCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(new[] { "start:1", "end:1", "start:2", "end:2" }, order.ToArray());
        Assert.Equal(2, host.GetDiagnostics().RequestsCompleted);
    }

    [Fact]
    public async Task AsyncRequests_FromDifferentSessionsCanRunConcurrently()
    {
        var listener = new InProcessChannelListener();
        var bothStarted = Completion();
        var release = Completion();
        var active = 0;
        var handler = new DelegateAsyncHandler(async (_, _, _, token) =>
        {
            if (Interlocked.Increment(ref active) == 2) bothStarted.TrySetResult();
            try { await release.Task.WaitAsync(token); }
            finally { Interlocked.Decrement(ref active); }
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var first = Connect(listener);
        using var second = Connect(listener);

        first.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        second.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);

        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, Volatile.Read(ref active));
        release.TrySetResult();
    }

    [Fact]
    public async Task Disconnect_CancelsActiveAndQueuedRequests()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var cancelled = Completion();
        var secondStarted = 0;
        var handler = new DelegateAsyncHandler(async (_, header, _, token) =>
        {
            if (header.Seq == 2)
            {
                Interlocked.Exchange(ref secondStarted, 1);
                return;
            }
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled.TrySetResult();
                throw;
            }
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);

        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);
        client.Close();

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(20);
        Assert.Equal(0, Volatile.Read(ref secondStarted));
        Assert.Equal(0, host.GetDiagnostics().RequestsFailed);
    }

    [Fact]
    public async Task StopAndRestart_CancelsOldQueueAndAcceptsNewRequests()
    {
        var listener = new InProcessChannelListener();
        var oldStarted = Completion();
        var oldCancelled = Completion();
        var newCompleted = Completion();
        var invocation = 0;
        var handler = new DelegateAsyncHandler(async (_, _, _, token) =>
        {
            if (Interlocked.Increment(ref invocation) == 1)
            {
                oldStarted.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    oldCancelled.TrySetResult();
                    throw;
                }
                return;
            }
            newCompleted.TrySetResult();
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var oldClient = Connect(listener);
        oldClient.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await oldStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        host.Stop();
        await oldCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        host.Start();
        using var newClient = Connect(listener);
        newClient.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);

        await newCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, host.GetDiagnostics().RequestsQueued);
        Assert.Equal(1, host.GetDiagnostics().RequestsCompleted);
        Assert.Equal(0, host.GetDiagnostics().RequestsFailed);
    }

    [Fact]
    public async Task PendingLimit_ClosesOnlyOverflowingSessionAndReportsRejection()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var release = Completion();
        var closed = Completion();
        var handler = new DelegateAsyncHandler(async (_, _, _, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var host = CreateHost(listener, handler, maxPending: 1);
        host.SessionClosed += _ => closed.TrySetResult();
        host.Start();
        using var client = Connect(listener);

        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, host.SessionCount);
        Assert.Equal(1, host.GetDiagnostics().RequestsRejected);
        release.TrySetResult();
    }

    [Fact]
    public async Task AsyncFailure_ReportsSessionAndUpdatesDiagnosticsWithoutBreakingQueue()
    {
        var listener = new InProcessChannelListener();
        var error = new TaskCompletionSource<(string SessionId, Exception Error)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCompleted = Completion();
        var handler = new DelegateAsyncHandler((_, header, _, _) =>
        {
            if (header.Seq == 1) throw new TestException();
            secondCompleted.TrySetResult();
            return Task.CompletedTask;
        });
        using var host = CreateHost(listener, handler);
        host.SessionError += (session, exception) => error.TrySetResult((session.Id, exception));
        host.Start();
        using var client = Connect(listener);

        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        client.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);

        var failure = await error.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await secondCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("1", failure.SessionId);
        Assert.IsType<TestException>(failure.Error);
        Assert.Equal(1, host.GetDiagnostics().RequestsFailed);
        Assert.Equal(1, host.GetDiagnostics().RequestsCompleted);
        Assert.Equal(1, host.SessionCount);
    }

    [Fact]
    public async Task Tick_ClosesIdleSessionWhileRecentInboundAndOutboundActivityDefersTimeout()
    {
        var clock = new FakeClock();
        var listener = new InProcessChannelListener();
        IServerNetworkSession? session = null;
        var opened = Completion();
        var closed = Completion();
        using var host = new NetworkHost(listener, new NetworkHostOptions
        {
            Clock = clock,
            IdleTimeout = TimeSpan.FromSeconds(10)
        });
        host.SessionOpened += value => { session = value; opened.TrySetResult(); };
        host.SessionClosed += _ => closed.TrySetResult();
        host.Start();
        using var client = Connect(listener);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(2));

        clock.Advance(TimeSpan.FromSeconds(9));
        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        clock.Advance(TimeSpan.FromSeconds(9));
        host.Tick();
        Assert.Equal(1, host.SessionCount);

        session!.SendPush(20, Bytes(2));
        clock.Advance(TimeSpan.FromSeconds(9));
        host.Tick();
        Assert.Equal(1, host.SessionCount);

        clock.Advance(TimeSpan.FromSeconds(1));
        host.Tick();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, host.GetDiagnostics().IdleTimeouts);
    }

    [Fact]
    public async Task SessionContextAndTrafficCounters_AreAvailableToApplicationCode()
    {
        var listener = new InProcessChannelListener();
        IServerNetworkSession? session = null;
        var handled = Completion();
        var handler = new DelegateAsyncHandler((value, header, payload, _) =>
        {
            session = value;
            value.Context.Set("account", "player-7");
            value.SendResponse(header.OpCode, header.Seq, payload);
            handled.TrySetResult();
            return Task.CompletedTask;
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);

        client.Send(10, Bytes(1, 2, 3), (ushort)NetworkPacketFlags.Request, 1);
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(session!.Context.TryGet<string>("account", out var account));
        Assert.Equal("player-7", account);
        Assert.True(session.Context.Remove("account"));
        Assert.False(session.Context.TryGet<string>("account", out _));
        Assert.True(session.BytesReceivedCount > 0);
        Assert.True(session.BytesSentCount > 0);
        Assert.Equal(1, session.PacketsReceivedCount);
        Assert.Equal(1, session.PacketsSentCount);
    }

    [Fact]
    public void AdmissionPolicy_RejectsBeforeSessionCreationWithStructuredReason()
    {
        var listener = new InProcessChannelListener();
        var policy = new RejectAllAdmissionPolicy("maintenance");
        using var host = new NetworkHost(listener, new NetworkHostOptions
        {
            AdmissionPolicy = policy
        });
        ChannelAdmissionResult rejection = default;
        host.ChannelRejected += (_, result) => rejection = result;
        host.Start();

        using var transport = listener.CreateClientTransport();

        Assert.False(rejection.Accepted);
        Assert.Equal("maintenance", rejection.Reason);
        Assert.Equal(0, host.SessionCount);
        Assert.Equal(1, host.GetDiagnostics().AdmissionRejections);
        Assert.Throws<InvalidOperationException>(() => transport.Connect("inprocess", 1));
    }

    [Fact]
    public void Tick_ClosesOnlySessionsThatMissEstablishmentDeadline()
    {
        var clock = new FakeClock();
        var listener = new InProcessChannelListener();
        var opened = new List<IServerNetworkSession>();
        using var host = new NetworkHost(listener, new NetworkHostOptions
        {
            Clock = clock,
            EstablishmentTimeout = TimeSpan.FromSeconds(5)
        });
        host.SessionOpened += session => opened.Add(session);
        host.Start();
        using var unestablished = Connect(listener);
        using var established = Connect(listener);
        opened[1].Context.MarkEstablished();

        clock.Advance(TimeSpan.FromSeconds(5));
        host.Tick();

        Assert.Single(host.GetSessionSnapshots());
        Assert.True(host.GetSessionSnapshots()[0].IsEstablished);
        Assert.Equal(1, host.GetDiagnostics().EstablishmentTimeouts);
    }

    [Fact]
    public async Task StopAsync_DrainsQueuedRequestsBeforeDisconnectingSessions()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var release = Completion();
        var handler = new DelegateAsyncHandler(async (_, _, _, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);
        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stopping = host.StopAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        Assert.False(stopping.IsCompleted);
        Assert.Equal(1, host.GetSessionSnapshots()[0].PendingRequests);

        release.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(2));

        var diagnostics = host.GetDiagnostics();
        Assert.Equal(0, host.SessionCount);
        Assert.Equal(1, diagnostics.GracefulStops);
        Assert.Equal(0, diagnostics.DrainTimeouts);
        Assert.Equal(0, diagnostics.RequestsCancelled);
    }

    [Fact]
    public async Task StopAsync_TimeoutCancelsActiveRequestAndReportsIt()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var cancelled = Completion();
        var handler = new DelegateAsyncHandler(async (_, _, _, token) =>
        {
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                cancelled.TrySetResult();
                throw;
            }
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);
        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await host.StopAsync(TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(2));
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var diagnostics = host.GetDiagnostics();
        Assert.Equal(1, diagnostics.DrainTimeouts);
        Assert.Equal(1, diagnostics.RequestsCancelled);
        Assert.Equal(0, diagnostics.RequestsFailed);
    }

    [Fact]
    public async Task SessionSnapshots_ExposeTransportStateAndPendingWork()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var release = Completion();
        var handler = new DelegateAsyncHandler(async (session, _, _, token) =>
        {
            session.Context.MarkEstablished();
            started.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var host = CreateHost(listener, handler);
        host.Start();
        using var client = Connect(listener);
        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var snapshot = Assert.Single(host.GetSessionSnapshots());
        Assert.Equal("1", snapshot.Id);
        Assert.Equal("inprocess-client", snapshot.RemoteEndpoint);
        Assert.True(snapshot.IsConnected);
        Assert.True(snapshot.IsEstablished);
        Assert.Equal(1, snapshot.PendingRequests);
        Assert.True(snapshot.BytesReceived > 0);
        Assert.True(snapshot.TimestampFrequency > 0);

        release.TrySetResult();
    }

    private static NetworkHost CreateHost(
        InProcessChannelListener listener,
        IAsyncServerRequestHandler handler,
        int maxPending = 256)
    {
        return new NetworkHost(listener, new NetworkHostOptions
        {
            AsyncRequestHandler = handler,
            MaxPendingRequestsPerSession = maxPending
        });
    }

    private static ConnectionManager Connect(InProcessChannelListener listener)
    {
        var client = new ConnectionManager(() => listener.CreateClientTransport(), new ConnectionOptions
        {
            EnableReconnect = false,
            HeartbeatInterval = TimeSpan.Zero,
            HeartbeatTimeout = TimeSpan.Zero
        });
        client.Open("inprocess", 1);
        return client;
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);
    private static TaskCompletionSource Completion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class DelegateAsyncHandler : IAsyncServerRequestHandler
    {
        private readonly Func<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>, CancellationToken, Task> _handle;

        public DelegateAsyncHandler(
            Func<IServerNetworkSession, NetworkPacketHeader, ArraySegment<byte>, CancellationToken, Task> handle)
        {
            _handle = handle;
        }

        public Task HandleAsync(
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken)
        {
            return _handle(session, header, payload, cancellationToken);
        }
    }

    private sealed class FakeClock : IMonotonicClock
    {
        public long Timestamp { get; private set; }
        public long Frequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration) => Timestamp += duration.Ticks;
    }

    private sealed class TestException : Exception
    {
    }

    private sealed class RejectAllAdmissionPolicy : IChannelAdmissionPolicy
    {
        private readonly string _reason;

        public RejectAllAdmissionPolicy(string reason) => _reason = reason;

        public ChannelAdmissionResult Evaluate(IServerChannel channel, int activeSessions)
        {
            return ChannelAdmissionResult.Reject(_reason);
        }
    }
}
