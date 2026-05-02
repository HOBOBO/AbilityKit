using System;

namespace AbilityKit.Protocol.Serialization
{
    public static class WireSerializer
    {
        private static IWireSerializer s_current;
        private static ITextSerializer s_textSerializer;

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

        public static ITextSerializer TextSerializer
        {
            get
            {
                if (s_textSerializer != null) return s_textSerializer;
                s_textSerializer = new JsonTextSerializer();
                return s_textSerializer;
            }
            set
            {
                s_textSerializer = value;
            }
        }

        public static byte[] Serialize<T>(in T value) => Current.Serialize(in value);

        public static T Deserialize<T>(byte[] bytes) => Current.Deserialize<T>(bytes);

        public static T Deserialize<T>(ReadOnlySpan<byte> bytes) => Current.Deserialize<T>(bytes);

        public static string SerializeToText<T>(T value, bool prettyPrint = false) =>
            TextSerializer.Serialize(value, prettyPrint);

        public static T DeserializeFromText<T>(string text) =>
            TextSerializer.Deserialize<T>(text);
    }
}
