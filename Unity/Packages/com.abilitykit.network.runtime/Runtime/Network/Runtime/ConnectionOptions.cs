#nullable enable

using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Observability;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 连接管理器创建传输会话时提供的输入。
    /// </summary>
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

    /// <summary>
    /// 连接管理器创建重连调度器时提供的输入。
    /// </summary>
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
        public bool ValidatePacketHeaders;

        public uint HeartbeatOpCode = 0;

        /// <summary>
        /// 可选传输会话工厂。连接管理器拥有并释放返回的会话；会话拥有工厂上下文提供的传输。
        /// </summary>
        public NetworkRuntimeSessionFactory? SessionFactory;

        /// <summary>
        /// 可选心跳中间件工厂。
        /// </summary>
        public NetworkHeartbeatMiddlewareFactory? HeartbeatFactory;

        /// <summary>
        /// 可选重连节奏工厂。
        /// </summary>
        public ReconnectAttemptSchedulerFactory? ReconnectSchedulerFactory;

        /// <summary>
        /// 可选数据包观测设置。包括重连在内的每次物理会话均解析新观察者，通过连接代际标识该会话。
        /// </summary>
        public NetworkTrafficCaptureOptions? TrafficCapture;

        /// <summary>
        /// 安装在默认网络会话上的可选目录驱动入站保护。每次重连会话重新创建，在数据包路由处理器前执行。
        /// </summary>
        public ProtocolPacketBoundaryValidator? PacketBoundaryValidator;

        public bool EnableKickHandling;
        public uint KickPushOpCode;
    }
}
