#nullable enable

using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;

namespace AbilityKit.Network.Runtime
{
    /// <summary>Inputs supplied when a connection manager creates a transport session.</summary>
    public readonly struct NetworkRuntimeSessionFactoryContext
    {
        internal NetworkRuntimeSessionFactoryContext(
            ITransport transport,
            IDispatcher callbackDispatcher,
            IDispatcher ioDispatcher,
            IFrameCodec frameCodec,
            ProtocolPacketBoundaryValidator? packetBoundaryValidator)
        {
            Transport = transport;
            CallbackDispatcher = callbackDispatcher;
            IoDispatcher = ioDispatcher;
            FrameCodec = frameCodec;
            PacketBoundaryValidator = packetBoundaryValidator;
        }

        public ITransport Transport { get; }
        public IDispatcher CallbackDispatcher { get; }
        public IDispatcher IoDispatcher { get; }
        public IFrameCodec FrameCodec { get; }
        public ProtocolPacketBoundaryValidator? PacketBoundaryValidator { get; }
    }

    /// <summary>Inputs supplied when a connection manager creates its reconnect scheduler.</summary>
    public readonly struct ReconnectAttemptSchedulerFactoryContext
    {
        internal ReconnectAttemptSchedulerFactoryContext(
            int maxAttempts,
            Func<int, float> resolveDelay)
        {
            MaxAttempts = maxAttempts;
            ResolveDelay = resolveDelay;
        }

        public int MaxAttempts { get; }
        public Func<int, float> ResolveDelay { get; }
    }

    public delegate INetworkRuntimeSession NetworkRuntimeSessionFactory(
        NetworkRuntimeSessionFactoryContext context);

    public delegate INetworkHeartbeatMiddleware NetworkHeartbeatMiddlewareFactory(
        uint heartbeatOpCode);

    public delegate Sync.IReconnectAttemptScheduler ReconnectAttemptSchedulerFactory(
        ReconnectAttemptSchedulerFactoryContext context);

    public sealed class ConnectionOptions
    {
        public IFrameCodec? FrameCodec;

        public TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);
        public TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);

        public bool EnableReconnect = true;
        public TimeSpan ReconnectInitialDelay = TimeSpan.FromSeconds(1);
        public TimeSpan ReconnectMaxDelay = TimeSpan.FromSeconds(10);
        public double ReconnectBackoffMultiplier = 1.5;
        public int ReconnectMaxAttempts = -1;

        public int MaxFrameLength = 4 * 1024 * 1024;

        public uint HeartbeatOpCode = 0;

        /// <summary>
        /// Optional transport-session factory. The connection manager owns and disposes the
        /// returned session; the session owns the transport supplied in the factory context.
        /// </summary>
        public NetworkRuntimeSessionFactory? SessionFactory;

        /// <summary>Optional heartbeat middleware factory.</summary>
        public NetworkHeartbeatMiddlewareFactory? HeartbeatFactory;

        /// <summary>Optional reconnect cadence factory.</summary>
        public ReconnectAttemptSchedulerFactory? ReconnectSchedulerFactory;

        /// <summary>
        /// Optional packet observation settings. A new observer is resolved for every physical
        /// session, including reconnects; the connection generation identifies that session.
        /// </summary>
        public NetworkTrafficCaptureOptions? TrafficCapture;

        /// <summary>
        /// Optional catalog-backed inbound guard installed on the default network session. It is
        /// recreated for every reconnect session and runs before packet route handlers.
        /// </summary>
        public ProtocolPacketBoundaryValidator? PacketBoundaryValidator;

        public bool EnableKickHandling;
        public uint KickPushOpCode;
    }
}
