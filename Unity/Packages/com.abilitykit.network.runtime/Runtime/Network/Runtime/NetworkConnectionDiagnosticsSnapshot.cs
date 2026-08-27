using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    /// <summary>Read-only runtime state exposed to diagnostics and editor tooling.</summary>
    public readonly struct NetworkConnectionDiagnosticsSnapshot
    {
        internal NetworkConnectionDiagnosticsSnapshot(
            string connectionId,
            int generation,
            string host,
            int port,
            ConnectionState state,
            bool isConnected,
            bool openRequested,
            bool reconnectPending,
            bool reconnectExhausted,
            int reconnectAttemptsStarted,
            int reconnectMaxAttempts,
            int nextReconnectAttempt,
            float nextReconnectDelaySeconds,
            float remainingReconnectDelaySeconds,
            float secondsSinceLastReceive,
            float secondsSinceLastHeartbeatSend,
            int pipelineMiddlewareCount,
            NetworkPacketRouterSnapshot? packetRouter)
        {
            ConnectionId = connectionId;
            Generation = generation;
            Host = host;
            Port = port;
            State = state;
            IsConnected = isConnected;
            OpenRequested = openRequested;
            ReconnectPending = reconnectPending;
            ReconnectExhausted = reconnectExhausted;
            ReconnectAttemptsStarted = reconnectAttemptsStarted;
            ReconnectMaxAttempts = reconnectMaxAttempts;
            NextReconnectAttempt = nextReconnectAttempt;
            NextReconnectDelaySeconds = nextReconnectDelaySeconds;
            RemainingReconnectDelaySeconds = remainingReconnectDelaySeconds;
            SecondsSinceLastReceive = secondsSinceLastReceive;
            SecondsSinceLastHeartbeatSend = secondsSinceLastHeartbeatSend;
            PipelineMiddlewareCount = pipelineMiddlewareCount;
            PacketRouter = packetRouter;
        }

        public string ConnectionId { get; }
        public int Generation { get; }
        public string Host { get; }
        public int Port { get; }
        public ConnectionState State { get; }
        public bool IsConnected { get; }
        public bool OpenRequested { get; }
        public bool ReconnectPending { get; }
        public bool ReconnectExhausted { get; }
        public int ReconnectAttemptsStarted { get; }
        public int ReconnectMaxAttempts { get; }
        public int NextReconnectAttempt { get; }
        public float NextReconnectDelaySeconds { get; }
        public float RemainingReconnectDelaySeconds { get; }
        public float SecondsSinceLastReceive { get; }
        public float SecondsSinceLastHeartbeatSend { get; }
        public int PipelineMiddlewareCount { get; }
        public NetworkPacketRouterSnapshot? PacketRouter { get; }
    }
}
