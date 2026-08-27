using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ConnectionRuntimeCompositionTests
{
    [Fact]
    public void Open_UsesConfiguredSessionHeartbeatAndReconnectSchedulerFactories()
    {
        var transport = new RecordingTransport();
        var heartbeat = new RecordingHeartbeatMiddleware();
        var scheduler = new RecordingReconnectScheduler();
        RecordingSession? session = null;
        NetworkRuntimeSessionFactoryContext sessionContext = default;
        ReconnectAttemptSchedulerFactoryContext schedulerContext = default;
        uint heartbeatOpCode = 0;
        NetworkPipeline? createdPipeline = null;

        var options = new ConnectionOptions
        {
            FrameCodec = LengthPrefixedFrameCodec.Instance,
            HeartbeatOpCode = 77,
            ReconnectInitialDelay = TimeSpan.FromSeconds(2),
            ReconnectMaxDelay = TimeSpan.FromSeconds(8),
            ReconnectBackoffMultiplier = 2d,
            ReconnectMaxAttempts = 3,
            SessionFactory = context =>
            {
                sessionContext = context;
                return session = new RecordingSession(context.Transport);
            },
            HeartbeatFactory = opCode =>
            {
                heartbeatOpCode = opCode;
                return heartbeat;
            },
            ReconnectSchedulerFactory = context =>
            {
                schedulerContext = context;
                return scheduler;
            }
        };

        var connection = new ConnectionManager(() => transport, options);
        connection.PipelineCreated += pipeline => createdPipeline = pipeline;

        connection.Open("gateway.example", 7100);

        Assert.Same(transport, sessionContext.Transport);
        Assert.Same(InlineDispatcher.Instance, sessionContext.CallbackDispatcher);
        Assert.Same(InlineDispatcher.Instance, sessionContext.IoDispatcher);
        Assert.Same(LengthPrefixedFrameCodec.Instance, sessionContext.FrameCodec);
        Assert.Equal(3, schedulerContext.MaxAttempts);
        Assert.Equal(2f, schedulerContext.ResolveDelay(0));
        Assert.Equal(8f, schedulerContext.ResolveDelay(3));
        Assert.Equal(77u, heartbeatOpCode);
        Assert.NotNull(session);
        Assert.True(session.Started);
        Assert.Same(session.Pipeline, createdPipeline);
        Assert.Equal("gateway.example", transport.Host);
        Assert.Equal(7100, transport.Port);

        connection.Dispose();

        Assert.True(session.Disposed);
        Assert.True(transport.Disposed);
        Assert.True(scheduler.ResetCount > 0);
    }

    [Fact]
    public void DiagnosticsSnapshot_ExposesConnectionAndRouterState()
    {
        var transport = new RecordingTransport();
        RecordingSession? session = null;
        var options = new ConnectionOptions
        {
            TrafficCapture = null,
            SessionFactory = context => session = new RecordingSession(context.Transport),
            ReconnectMaxAttempts = 3,
            HeartbeatInterval = TimeSpan.FromSeconds(5),
            HeartbeatTimeout = TimeSpan.FromSeconds(15)
        };
        using var connection = new ConnectionManager(() => transport, options);

        var disconnected = connection.GetDiagnosticsSnapshot();
        Assert.Equal(ConnectionState.Disconnected, disconnected.State);
        Assert.Equal(0, disconnected.Generation);
        Assert.Null(disconnected.PacketRouter);

        connection.Open("gateway.example", 7100);
        var routeHandler = (NetworkPacketRouteHandler)(_ => { });
        session!.PacketRouter.Register(17, NetworkPacketDispatchKind.ServerPush, routeHandler);

        var snapshot = connection.GetDiagnosticsSnapshot();
        Assert.Equal("gateway.example", snapshot.Host);
        Assert.Equal(7100, snapshot.Port);
        Assert.Equal(1, snapshot.Generation);
        Assert.Equal(ConnectionState.Connecting, snapshot.State);
        Assert.True(snapshot.OpenRequested);
        Assert.NotNull(snapshot.PacketRouter);
        Assert.Single(snapshot.PacketRouter!.Value.Routes);
        Assert.Equal(1, snapshot.PacketRouter.Value.Routes[0].HandlerCount);
        Assert.Same(session.PacketRouter, connection.PacketRouter);
    }

    [Fact]
    public void Open_WhenSessionFactoryReturnsNull_DisposesCreatedTransport()
    {
        var transport = new RecordingTransport();
        using var connection = new ConnectionManager(
            () => transport,
            new ConnectionOptions { SessionFactory = _ => null! });

        var exception = Assert.Throws<InvalidOperationException>(
            () => connection.Open("gateway.example", 7100));

        Assert.Equal("Network session factory returned null.", exception.Message);
        Assert.True(transport.Disposed);
        Assert.Equal(ConnectionState.Disconnected, connection.State);
    }

    [Fact]
    public void Constructor_WhenReconnectSchedulerFactoryReturnsNull_RejectsConfiguration()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ConnectionManager(
                () => new RecordingTransport(),
                new ConnectionOptions { ReconnectSchedulerFactory = _ => null! }));

        Assert.Equal("Reconnect scheduler factory returned null.", exception.Message);
    }

    private sealed class RecordingSession : INetworkRuntimeSession
    {
        private readonly ITransport _transport;

        public RecordingSession(ITransport transport)
        {
            _transport = transport;
        }

        public bool IsConnected => _transport.IsConnected;
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public NetworkPipeline Pipeline { get; } = new();
        public NetworkPacketRouter PacketRouter { get; } = new();

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public void Start() => Started = true;
        public void Stop() => Started = false;

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
        }

        public void Dispose()
        {
            Disposed = true;
            Started = false;
            _transport.Dispose();
        }
    }

    private sealed class RecordingHeartbeatMiddleware : INetworkHeartbeatMiddleware
    {
        public event Action? HeartbeatReceived;

        public void OnInbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next) => next(header, payload);

        public void OnOutbound(
            ISessionContext context,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            Action<NetworkPacketHeader, ArraySegment<byte>> next) => next(header, payload);
    }

    private sealed class RecordingReconnectScheduler : IReconnectAttemptScheduler
    {
        public bool IsPending { get; private set; }
        public bool IsExhausted { get; private set; }
        public int AttemptsStarted { get; private set; }
        public int MaxAttempts => 3;
        public int NextAttemptNumber => AttemptsStarted + 1;
        public float NextDelaySeconds => 0f;
        public float RemainingDelaySeconds => 0f;
        public int ResetCount { get; private set; }

        public bool Request()
        {
            IsPending = true;
            return true;
        }

        public bool TryTakeAttempt(float deltaTime, out int attemptNumber)
        {
            attemptNumber = 0;
            return false;
        }

        public void Reset()
        {
            ResetCount++;
            IsPending = false;
            IsExhausted = false;
            AttemptsStarted = 0;
        }
    }

    private sealed class RecordingTransport : ITransport
    {
        public bool IsConnected { get; private set; }
        public bool Disposed { get; private set; }
        public string? Host { get; private set; }
        public int Port { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;

        public void Connect(string host, int port)
        {
            Host = host;
            Port = port;
            IsConnected = true;
        }

        public void Close() => IsConnected = false;
        public void Send(ArraySegment<byte> bytes) { }

        public void Dispose()
        {
            Disposed = true;
            IsConnected = false;
        }
    }
}
