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

        public Func<object, ArraySegment<byte>> SerializeCreateWorld;
        public Func<object, ArraySegment<byte>> SerializeJoin;
        public Func<object, ArraySegment<byte>> SerializeLeave;
        public Func<object, ArraySegment<byte>> SerializeSubmitInput;
        public Func<object, int, object> RewriteSubmitInputFrame;
        public Func<ArraySegment<byte>, NetworkSubmitInputResponse> DeserializeSubmitInputResponse;

        public Func<ArraySegment<byte>, AbilityKit.Ability.Host.FramePacket> DeserializeFramePushed;
		public Func<ArraySegment<byte>, object> DeserializeSnapshotPushed;
		public Action<int> OnSubmitInputAck;
    }

    public readonly struct NetworkSubmitInputResponse
    {
        public readonly bool Accepted;
        public readonly int ServerFrame;
        public readonly int ReasonCode;
        public readonly bool RetryAtAuthoritativeFrame;

        public NetworkSubmitInputResponse(
            bool accepted,
            int serverFrame,
            int reasonCode,
            bool retryAtAuthoritativeFrame)
        {
            Accepted = accepted;
            ServerFrame = serverFrame;
            ReasonCode = reasonCode;
            RetryAtAuthoritativeFrame = retryAtAuthoritativeFrame;
        }
    }
}
