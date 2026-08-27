using System;
using System.Threading;

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
                var current = Volatile.Read(ref s_current);
                if (current != null) return current;
                throw new InvalidOperationException(
                    "WireSerializer.Current is not installed. " +
                    "Install an IWireSerializer explicitly during application bootstrap " +
                    "by calling WireSerializer.Install(...) or a codec-specific installer.");
            }
            set
            {
                Volatile.Write(ref s_current, value);
            }
        }

        public static bool IsInstalled => Volatile.Read(ref s_current) != null;

        public static bool TryGetCurrent(out IWireSerializer serializer)
        {
            serializer = Volatile.Read(ref s_current);
            return serializer != null;
        }

        public static void Install(IWireSerializer serializer, bool replaceExisting = false)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (replaceExisting)
            {
                Volatile.Write(ref s_current, serializer);
                return;
            }

            var previous = Interlocked.CompareExchange(ref s_current, serializer, null);
            if (previous != null)
                throw new InvalidOperationException(
                    "A wire serializer is already installed. Pass replaceExisting: true only from an explicit reconfiguration boundary.");
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
