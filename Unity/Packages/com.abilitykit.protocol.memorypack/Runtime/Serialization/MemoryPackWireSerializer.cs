using System;
using System.Linq;
using System.Reflection;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.MemoryPack
{
    public sealed class MemoryPackWireSerializer : IWireSerializer
    {
        private static readonly Type SerializerType = FindSerializerType();

        private static Type FindSerializerType()
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

        private static MethodInfo GetSerializeMethod(Type serializerType, Type valueType)
        {
            var methods = serializerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (!string.Equals(m.Name, "Serialize", StringComparison.Ordinal)) continue;
                if (!m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                if (ps[0].ParameterType != valueType) continue;
                return m.MakeGenericMethod(valueType);
            }

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (!string.Equals(m.Name, "Serialize", StringComparison.Ordinal)) continue;
                if (!m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                if (ps[0].ParameterType.IsByRef && ps[0].ParameterType.GetElementType() == valueType)
                {
                    return m.MakeGenericMethod(valueType);
                }
            }

            return null;
        }

        private static MethodInfo GetDeserializeMethod(Type serializerType, Type valueType)
        {
            var methods = serializerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (!string.Equals(m.Name, "Deserialize", StringComparison.Ordinal)) continue;
                if (!m.IsGenericMethodDefinition) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1) continue;
                if (ps[0].ParameterType != typeof(byte[])) continue;
                return m.MakeGenericMethod(valueType);
            }
            return null;
        }

        public byte[] Serialize<T>(in T value)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available. Add MemoryPack DLL to this Unity package (Runtime/Plugins) or install via NuGet on server side.");

            var m = GetSerializeMethod(t, typeof(T));
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Serialize<T>(T) not found.");

            var result = m.Invoke(null, new object[] { value });
            return (byte[])result;
        }

        public T Deserialize<T>(byte[] bytes)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available. Add MemoryPack DLL to this Unity package (Runtime/Plugins) or install via NuGet on server side.");

            var m = GetDeserializeMethod(t, typeof(T));
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Deserialize<T>(byte[]) not found.");

            var result = m.Invoke(null, new object[] { bytes });
            return (T)result;
        }

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
        {
            return Deserialize<T>(bytes.ToArray());
        }
    }
}
