using MemoryPack;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Coordinator
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CoordinatorPayloadAttribute : Attribute
    {
        public int OpCode { get; }

        public CoordinatorPayloadAttribute(int opCode)
        {
            OpCode = opCode;
        }
    }

    public static class CoordinatorPayloadCodec
    {
        private static readonly ConcurrentDictionary<Type, int> s_opCodes = new ConcurrentDictionary<Type, int>();

        public static byte[] Encode<T>(in T payload)
        {
            return MemoryPackSerializer.Serialize(payload);
        }

        public static T Decode<T>(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return default;
            }

            return MemoryPackSerializer.Deserialize<T>(payload);
        }

        public static bool TryDecode<T>(byte[] payload, out T value)
        {
            value = default;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            value = MemoryPackSerializer.Deserialize<T>(payload);
            return true;
        }

        public static int GetOpCode<T>()
        {
            return GetOpCode(typeof(T));
        }

        public static int GetOpCode(Type payloadType)
        {
            if (payloadType == null)
            {
                throw new ArgumentNullException(nameof(payloadType));
            }

            return s_opCodes.GetOrAdd(payloadType, ResolveOpCode);
        }

        public static bool TryGetOpCode<T>(out int opCode)
        {
            return TryGetOpCode(typeof(T), out opCode);
        }

        public static bool TryGetOpCode(Type payloadType, out int opCode)
        {
            opCode = 0;
            if (payloadType == null)
            {
                return false;
            }

            var attr = payloadType.GetCustomAttribute<CoordinatorPayloadAttribute>(false);
            if (attr == null)
            {
                return false;
            }

            opCode = attr.OpCode;
            s_opCodes.TryAdd(payloadType, opCode);
            return true;
        }

        private static int ResolveOpCode(Type payloadType)
        {
            var attr = payloadType.GetCustomAttribute<CoordinatorPayloadAttribute>(false);
            if (attr == null)
            {
                throw new InvalidOperationException($"Coordinator payload type '{payloadType.FullName}' is missing CoordinatorPayloadAttribute.");
            }

            return attr.OpCode;
        }
    }
}
