using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol
{
    /// <summary>
    /// 协议注册表：只负责 OpCode 与类型的双向映射（纯注册表职责）。
    ///
    /// 提供：
    /// 1. 基于 ProtocolOpCodeAttribute 的自动类型注册（幂等，可重复扫描）
    /// 2. OpCode 与类型的双向映射查询
    /// 3. 编解码便捷方法（委托给 <see cref="WireSerializer"/>，单一序列化真相源）
    ///
    /// 序列化器由 <see cref="WireSerializer.Current"/> 决定（单一决策点），本注册表不再持有序列化器。
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
        private bool _isScanned;

        private ProtocolRegistry()
        {
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
        /// 注册单个类型。幂等：同一类型重复注册（如重复扫描）是 no-op；
        /// 仅当同一 OpCode 被映射到不同类型时才抛冲突。
        /// </summary>
        public void RegisterType(Type type)
        {
            if (type == null) return;

            var attr = type.GetCustomAttribute<ProtocolOpCodeAttribute>();
            if (attr == null) return;

            if (_opCodeToType.TryGetValue(attr.OpCode, out var existingType))
            {
                if (existingType == type)
                    return;
                throw new InvalidOperationException($"Duplicate OpCode {attr.OpCode}: {existingType.FullName} vs {type.FullName}");
            }

            _opCodeToType[attr.OpCode] = type;
            _typeToOpCode[type] = attr.OpCode;
        }

        /// <summary>
        /// 泛型编码（委托给 WireSerializer）
        /// </summary>
        public byte[] Encode<T>(in T value) where T : struct
        {
            return WireSerializer.Serialize(in value);
        }

        /// <summary>
        /// 非泛型编码（委托给 WireSerializer）
        /// </summary>
        public byte[] Encode(object value)
        {
            return WireSerializer.Serialize(value);
        }

        /// <summary>
        /// 泛型解码（委托给 WireSerializer）
        /// </summary>
        public T Decode<T>(byte[] payload) where T : struct
        {
            if (payload == null || payload.Length == 0)
            {
                throw new ArgumentException("Payload cannot be null or empty", nameof(payload));
            }
            return WireSerializer.Deserialize<T>(payload);
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
        /// 根据 OpCode 解码为指定类型：未注册或类型不匹配都会拒绝。
        /// </summary>
        public T DecodeByOpCode<T>(uint opCode, byte[] payload) where T : struct
        {
            var registeredType = GetType(opCode);
            if (registeredType == null)
            {
                throw new InvalidOperationException($"OpCode {opCode} is not registered.");
            }
            if (registeredType != typeof(T))
            {
                throw new InvalidOperationException($"OpCode {opCode} is registered for type {registeredType.FullName}, but trying to decode as {typeof(T).FullName}");
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
}
