using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol
{
    /// <summary>
    /// 协议注册表
    /// 
    /// 提供：
    /// 1. 基于 ProtocolOpCodeAttribute 的自动类型注册
    /// 2. 泛型编解码接口
    /// 3. OpCode 与类型的双向映射
    /// 
    /// 使用方式：
    /// <code>
    /// // 启动时扫描程序集
    /// ProtocolRegistry.Instance.ScanAssembly(typeof(ProtocolRegistry).Assembly);
    /// 
    /// // 编码
    /// var payload = ProtocolRegistry.Encode(new MyRequest { ... });
    /// 
    /// // 解码
    /// var request = ProtocolRegistry.Decode&lt;MyRequest&gt;(payload);
    /// 
    /// // 根据 OpCode 获取类型
    /// var type = ProtocolRegistry.Instance.GetType(100);
    /// </code>
    /// </summary>
    public sealed class ProtocolRegistry
    {
        private static readonly Lazy<ProtocolRegistry> _instance = new(() => new ProtocolRegistry());
        
        /// <summary>
        /// 单例实例
        /// </summary>
        public static ProtocolRegistry Instance => _instance.Value;

        private readonly Dictionary<uint, Type> _opCodeToType = new();
        private readonly Dictionary<Type, uint> _typeToOpCode = new();
        private IWireSerializer _serializer;
        private bool _isScanned;

        private ProtocolRegistry()
        {
            _serializer = CreateDefaultSerializer();
        }

        private static IWireSerializer CreateDefaultSerializer()
        {
            return new MemoryPackWireSerializer();
        }

        /// <summary>
        /// 设置序列化器
        /// </summary>
        public void SetSerializer(IWireSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// 扫描程序集并注册所有带 ProtocolOpCodeAttribute 的类型
        /// </summary>
        public void ScanAssembly(Assembly assembly)
        {
            if (assembly == null) return;

            foreach (var type in assembly.GetTypes())
            {
                RegisterType(type);
            }
            _isScanned = true;
        }

        /// <summary>
        /// 扫描程序集并注册所有带 ProtocolOpCodeAttribute 的类型
        /// </summary>
        public void ScanAssembly(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                ScanAssembly(assembly);
            }
        }

        /// <summary>
        /// 注册单个类型
        /// </summary>
        public void RegisterType(Type type)
        {
            if (type == null) return;

            var attr = type.GetCustomAttribute<ProtocolOpCodeAttribute>();
            if (attr == null) return;

            if (_opCodeToType.ContainsKey(attr.OpCode))
            {
                throw new InvalidOperationException($"Duplicate OpCode {attr.OpCode} for type {type.FullName}. Existing type: {_opCodeToType[attr.OpCode].FullName}");
            }

            _opCodeToType[attr.OpCode] = type;
            _typeToOpCode[type] = attr.OpCode;
        }

        /// <summary>
        /// 泛型编码
        /// </summary>
        public byte[] Encode<T>(in T value) where T : struct
        {
            return _serializer.Serialize(value);
        }

        /// <summary>
        /// 非泛型编码
        /// </summary>
        public byte[] Encode(object value)
        {
            var bytes = _serializer.Serialize(value);
            return bytes;
        }

        /// <summary>
        /// 泛型解码
        /// </summary>
        public T Decode<T>(byte[] payload) where T : struct
        {
            if (payload == null || payload.Length == 0)
            {
                throw new ArgumentException("Payload cannot be null or empty", nameof(payload));
            }
            return _serializer.Deserialize<T>(payload);
        }

        /// <summary>
        /// 根据 OpCode 获取类型
        /// </summary>
        public Type? GetType(uint opCode)
        {
            return _opCodeToType.TryGetValue(opCode, out var type) ? type : null;
        }

        /// <summary>
        /// 根据类型获取 OpCode
        /// </summary>
        public uint? GetOpCode<T>()
        {
            return _typeToOpCode.TryGetValue(typeof(T), out var opCode) ? opCode : null;
        }

        /// <summary>
        /// 根据 OpCode 获取类型，并解码为指定类型
        /// </summary>
        public T DecodeByOpCode<T>(uint opCode, byte[] payload) where T : struct
        {
            var expectedType = typeof(T);
            var registeredType = GetType(opCode);

            if (registeredType != null && registeredType != expectedType)
            {
                throw new InvalidOperationException($"OpCode {opCode} is registered for type {registeredType.FullName}, but trying to decode as {expectedType.FullName}");
            }

            return Decode<T>(payload);
        }

        /// <summary>
        /// 获取所有已注册的 OpCode
        /// </summary>
        public IReadOnlyCollection<uint> GetAllOpCodes()
        {
            return _opCodeToType.Keys;
        }

        /// <summary>
        /// 检查是否已扫描
        /// </summary>
        public bool IsScanned => _isScanned;

        /// <summary>
        /// 获取协议方向
        /// </summary>
        public ProtocolDirection? GetDirection(uint opCode)
        {
            var type = GetType(opCode);
            if (type == null) return null;

            var attr = type.GetCustomAttribute<ProtocolOpCodeAttribute>();
            return attr?.Direction;
        }

        /// <summary>
        /// 清空注册表（通常用于测试）
        /// </summary>
        public void Clear()
        {
            _opCodeToType.Clear();
            _typeToOpCode.Clear();
            _isScanned = false;
        }
    }

    /// <summary>
    /// MemoryPack 序列化器实现
    /// </summary>
    internal sealed class MemoryPackWireSerializer : IWireSerializer
    {
        private static readonly Type SerializerType = FindSerializerType();

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
