using System;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Battle.Transport
{
    public sealed class GatewayBattleLogicTransportOptions
    {
        public string Host = "127.0.0.1";
        public int Port = 0;

        public Func<ITransport> TransportFactory;

        public IFrameCodec FrameCodec;

        public uint OpCreateWorld;
        public uint OpJoin;
        public uint OpLeave;
        public uint OpSubmitInput;

        public uint OpFramePushed;

        public Func<object, ArraySegment<byte>> SerializeCreateWorld;
        public Func<object, ArraySegment<byte>> SerializeJoin;
        public Func<object, ArraySegment<byte>> SerializeLeave;
        public Func<object, ArraySegment<byte>> SerializeSubmitInput;

        public Func<ArraySegment<byte>, AbilityKit.Ability.Server.FramePacket> DeserializeFramePushed;
    }
}
