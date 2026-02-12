using System;
using AbilityKit.Ability.Share;

namespace AbilityKit.Protocol.Serialization
{
    public sealed class BinaryObjectWireSerializer : IWireSerializer
    {
        public byte[] Serialize<T>(in T value)
        {
            return BinaryObjectCodec.Encode(value);
        }

        public T Deserialize<T>(byte[] bytes)
        {
            return BinaryObjectCodec.Decode<T>(bytes);
        }

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
        {
            return BinaryObjectCodec.Decode<T>(bytes.ToArray());
        }
    }
}
