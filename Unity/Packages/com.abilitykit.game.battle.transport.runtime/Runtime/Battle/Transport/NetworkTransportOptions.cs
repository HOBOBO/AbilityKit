using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle.Transport
{
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

        public IFrameCodec FrameCodec;

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
    }

    public readonly struct NetworkSubmitInputResponse
    {
        public readonly bool Accepted;
        public readonly int ServerFrame;
        public readonly int ReasonCode;
        public readonly bool RetryAtAuthoritativeFrame;
        public readonly string Status;
        public readonly string Message;

        public NetworkSubmitInputResponse(
            bool accepted,
            int serverFrame,
            int reasonCode,
            bool retryAtAuthoritativeFrame,
            string status = null,
            string message = null)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            ReasonCode = reasonCode;
            RetryAtAuthoritativeFrame = retryAtAuthoritativeFrame;
            Status = status ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }
}
