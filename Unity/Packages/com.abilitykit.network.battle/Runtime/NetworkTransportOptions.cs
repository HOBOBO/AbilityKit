using System;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Observability;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Network.Battle
{
    /// <summary>Controls whether a prebuilt SDK client is borrowed or disposed with the battle transport.</summary>
    public enum NetworkSdkClientOwnership
    {
        Borrowed = 0,
        Owned = 1
    }

    public sealed class NetworkTransportOptions
    {
        public string Host = "127.0.0.1";
        public int Port = 0;

        public uint OpRenewSession;
        public string SessionToken;
        public Func<string, ArraySegment<byte>> SerializeRenewSession;
        public uint OpPostAuthentication;
        public Func<ArraySegment<byte>> SerializePostAuthentication;
        public Func<string, long, ArraySegment<byte>> SerializePostAuthenticationWithReliableEventCursor;
        public Func<string> GetReliableEventEpoch;
        public Func<long> GetReliableEventLastAcknowledgedSequence;

        public Func<ITransport> TransportFactory;

        /// <summary>
        /// Optional: inject an existing <c>IConnection</c> (e.g. shared room+battle single-socket topology).
        /// If set, <see cref="TransportFactory"/> is ignored and <see cref="Host"/>/<see cref="Port"/> need not be set.
        /// </summary>
        public Func<IConnection> ConnectionFactory;

        /// <summary>
        /// Optional prebuilt SDK client. When set, connection and transport factories are ignored.
        /// The default ownership is <see cref="NetworkSdkClientOwnership.Borrowed"/>.
        /// </summary>
        public NetworkSdkClient SdkClient;

        /// <summary>
        /// Optional SDK client factory. Its returned client is owned by the battle transport.
        /// It cannot be combined with <see cref="SdkClient"/>.
        /// </summary>
        public Func<NetworkSdkClient> SdkClientFactory;

        /// <summary>Ownership applied only to <see cref="SdkClient"/>.</summary>
        public NetworkSdkClientOwnership SdkClientOwnership;

        public IFrameCodec FrameCodec;

        /// <summary>
        /// Configures the complete connection runtime when the battle transport builds its own SDK client.
        /// Set to null to use SDK defaults. The initial value preserves the historical Battle reconnect preset.
        /// </summary>
        public Action<ConnectionOptions> ConfigureConnection = ApplyBattleConnectionDefaults;

        /// <summary>
        /// Optional packet observer installed only when this transport builds its SDK client from
        /// <see cref="TransportFactory"/>. Prebuilt SDK clients and injected connections must
        /// configure observation at their own composition boundary.
        /// </summary>
        public INetworkTrafficObserver TrafficObserver;

        /// <summary>Configures metadata and payload-preview policy for <see cref="TrafficObserver"/>.</summary>
        public Action<NetworkTrafficCaptureOptions> ConfigureTrafficCapture;

        public uint OpCreateWorld;
        public uint OpJoin;
        public uint OpLeave;
        public uint OpSubmitInput;
        public int SubmitInputRetryFrameLead = 2;

        public uint OpFramePushed;
        public uint OpSnapshotPushed;
        public uint OpDeltaSnapshotPushed;
        public uint OpReliableEventsPushed;
        public uint OpAcknowledgeReliableEvents;
        public uint OpRequestFullStateSync;

        public Func<object, ArraySegment<byte>> SerializeCreateWorld;
        public Func<object, ArraySegment<byte>> SerializeJoin;
        public Func<object, ArraySegment<byte>> SerializeLeave;
        public Func<object, object> PrepareSubmitInput;
        public Func<object, ArraySegment<byte>> SerializeSubmitInput;
        public Func<object, int, object> RewriteSubmitInputFrame;
        public Func<ArraySegment<byte>, NetworkSubmitInputResponse> DeserializeSubmitInputResponse;

        public Func<ArraySegment<byte>, AbilityKit.Ability.Host.FramePacket> DeserializeFramePushed;
        public Func<ArraySegment<byte>, object> DeserializeSnapshotPushed;
        public Func<ArraySegment<byte>, object> DeserializeReliableEventsPushed;
        public Func<string, long, ArraySegment<byte>> SerializeAcknowledgeReliableEvents;
        public Func<ArraySegment<byte>, long> DeserializeAcknowledgeReliableEventsResponse;
        public Func<string, int, ArraySegment<byte>> SerializeRequestFullStateSync;
        public Func<ArraySegment<byte>, bool> DeserializeRequestFullStateSyncResponse;
        public Action<int> OnSubmitInputAck;

        public bool HasSdkClientSource => SdkClient != null || SdkClientFactory != null;

        /// <summary>Applies the legacy Battle reconnect cadence without coupling it to NetworkTransport.</summary>
        public static void ApplyBattleConnectionDefaults(ConnectionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.EnableReconnect = true;
            options.ReconnectInitialDelay = TimeSpan.FromSeconds(ReconnectBackoffPolicy.BaseDelaySeconds);
            options.ReconnectMaxDelay = TimeSpan.FromSeconds(ReconnectBackoffPolicy.MaxDelaySeconds);
            options.ReconnectBackoffMultiplier = 2d;
            options.ReconnectMaxAttempts = ReconnectBackoffPolicy.MaxAttempts;
        }
    }

    public readonly struct NetworkSubmitInputResponse
    {
        public readonly bool Accepted;
        public readonly int ServerFrame;
        public readonly int ReasonCode;
        public readonly bool RetryAtAuthoritativeFrame;
        public readonly string Status;
        public readonly string Message;
        /// <summary>The frame the server accepted the input at (statesync lag-compensation health).</summary>
        public readonly int AcceptedFrame;
        /// <summary>Server-side timestamp (UTC ticks) at accept, for lag-compensation validation.</summary>
        public readonly long ServerTicks;
        /// <summary>Server signaled that the client should request a full-state resync.</summary>
        public readonly bool ShouldResync;

        public NetworkSubmitInputResponse(
            bool accepted,
            int serverFrame,
            int reasonCode,
            bool retryAtAuthoritativeFrame,
            string status = null,
            string message = null,
            int acceptedFrame = 0,
            long serverTicks = 0,
            bool shouldResync = false)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            ReasonCode = reasonCode;
            RetryAtAuthoritativeFrame = retryAtAuthoritativeFrame;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
            AcceptedFrame = acceptedFrame;
            ServerTicks = serverTicks;
            ShouldResync = shouldResync;
        }
    }
}
