using System;

namespace AbilityKit.Protocol.Serialization
{
    public interface IWireSerializer
    {
        byte[] Serialize<T>(in T value);

        T Deserialize<T>(byte[] bytes);

        T Deserialize<T>(ReadOnlySpan<byte> bytes);
    }
}
