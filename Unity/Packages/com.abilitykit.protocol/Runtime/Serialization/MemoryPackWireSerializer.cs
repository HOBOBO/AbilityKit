using System;
using System.Reflection;

namespace AbilityKit.Protocol.Serialization
{
    /// <summary>
    /// 基于反射的 MemoryPack 序列化实现。
    /// protocol 包本身不依赖 MemoryPack；运行时若加载了 MemoryPack 则通过反射调用其
    /// MemoryPackSerializer.Serialize/Deserialize，否则抛异常。
    /// </summary>
    internal sealed class MemoryPackWireSerializer : IWireSerializer
    {
        private static readonly Type? SerializerType = FindSerializerType();

        private static Type? FindSerializerType()
        {
            try
            {
                var direct = Type.GetType("MemoryPack.MemoryPackSerializer", throwOnError: false);
                if (direct != null) return direct;

                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var t = asms[i].GetType("MemoryPack.MemoryPackSerializer", throwOnError: false);
                    if (t != null) return t;
                }
            }
            catch
            {
            }
            return null;
        }

        public byte[] Serialize<T>(in T value)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available.");

            var method = t.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static);
            if (method != null && method.IsGenericMethodDefinition)
            {
                method = method.MakeGenericMethod(typeof(T));
                return (byte[])method.Invoke(null, new object[] { value })!;
            }

            throw new InvalidOperationException("MemoryPackSerializer.Serialize<T> not found.");
        }

        public T Deserialize<T>(byte[] bytes)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available.");

            var method = t.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
            if (method != null && method.IsGenericMethodDefinition)
            {
                method = method.MakeGenericMethod(typeof(T));
                return (T)method.Invoke(null, new object[] { bytes })!;
            }

            throw new InvalidOperationException("MemoryPackSerializer.Deserialize<T> not found.");
        }

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
        {
            return Deserialize<T>(bytes.ToArray());
        }
    }
}
