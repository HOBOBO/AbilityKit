using System;

namespace AbilityKit.Protocol.Serialization
{
    public static class WireSerializer
    {
        private static IWireSerializer s_current;

        public static IWireSerializer Current
        {
            get
            {
                if (s_current != null) return s_current;
                s_current = new BinaryObjectWireSerializer();
                return s_current;
            }
            set
            {
                s_current = value;
            }
        }

        public static byte[] Serialize<T>(in T value) => Current.Serialize(in value);

        public static T Deserialize<T>(byte[] bytes) => Current.Deserialize<T>(bytes);

        public static T Deserialize<T>(ReadOnlySpan<byte> bytes) => Current.Deserialize<T>(bytes);
    }
}
