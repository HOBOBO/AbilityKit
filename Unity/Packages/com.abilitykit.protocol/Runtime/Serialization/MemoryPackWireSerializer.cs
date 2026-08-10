using System;
using System.Buffers;
using System.Reflection;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Serialization
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
                var method = methods[i];
                if (!string.Equals(method.Name, "Serialize", StringComparison.Ordinal)) continue;
                if (!method.IsGenericMethodDefinition) continue;
                if (method.ReturnType != typeof(byte[])) continue;

                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length != 1) continue;

                var parameters = method.GetParameters();
                if (parameters.Length < 1 || !AreRemainingParametersOptional(parameters, 1)) continue;

                var parameterType = UnwrapByRef(parameters[0].ParameterType);
                if (parameterType != genericArguments[0]) continue;
                return method.MakeGenericMethod(valueType);
            }

            return null;
        }

        private static MethodInfo GetDeserializeMethod(Type serializerType, Type valueType)
        {
            var methods = serializerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (!string.Equals(method.Name, "Deserialize", StringComparison.Ordinal)) continue;
                if (!method.IsGenericMethodDefinition) continue;

                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length != 1) continue;
                if (method.ReturnType != genericArguments[0]) continue;

                var parameters = method.GetParameters();
                if (parameters.Length < 1 || !AreRemainingParametersOptional(parameters, 1)) continue;
                if (UnwrapByRef(parameters[0].ParameterType) != typeof(ReadOnlySequence<byte>)) continue;
                return method.MakeGenericMethod(valueType);
            }

            return null;
        }

        private static Type UnwrapByRef(Type type)
        {
            return type.IsByRef ? type.GetElementType() : type;
        }

        private static bool AreRemainingParametersOptional(ParameterInfo[] parameters, int startIndex)
        {
            for (int i = startIndex; i < parameters.Length; i++)
            {
                if (!parameters[i].IsOptional) return false;
            }

            return true;
        }

        private static object[] CreateInvokeArguments(MethodInfo method, object firstArgument)
        {
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            arguments[0] = firstArgument;
            for (int i = 1; i < arguments.Length; i++)
            {
                arguments[i] = Type.Missing;
            }

            return arguments;
        }

        public byte[] Serialize<T>(in T value)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available. Add MemoryPack DLL to this Unity package (Runtime/Plugins) or install via NuGet on server side.");

            var m = GetSerializeMethod(t, typeof(T));
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Serialize<T>(T, options) not found.");

            var result = m.Invoke(null, CreateInvokeArguments(m, value));
            return (byte[])result;
        }

        public T Deserialize<T>(byte[] bytes)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available. Add MemoryPack DLL to this Unity package (Runtime/Plugins) or install via NuGet on server side.");

            var m = GetDeserializeMethod(t, typeof(T));
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Deserialize<T>(ReadOnlySequence<byte>, options) not found.");

            var sequence = new ReadOnlySequence<byte>(bytes);
            var result = m.Invoke(null, CreateInvokeArguments(m, sequence));
            return (T)result;
        }

        public T Deserialize<T>(ReadOnlySpan<byte> bytes)
        {
            return Deserialize<T>(bytes.ToArray());
        }
    }
}
