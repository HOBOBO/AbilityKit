using System;
using System.Reflection;

namespace AbilityKit.Protocol
{
    /// <summary>
    /// 协议传输方向
    /// </summary>
    public enum ProtocolDirection
    {
        /// <summary>
        /// 客户端到服务器
        /// </summary>
        ClientToServer,

        /// <summary>
        /// 服务器到客户端
        /// </summary>
        ServerToClient,

        /// <summary>
        /// 双向协议（通常用于服务器推送）
        /// </summary>
        Bidirectional
    }

    /// <summary>
    /// 标记协议类型及其 OpCode
    /// 
    /// 使用方式：
    /// 1. 在协议结构上标记 [ProtocolOpCode] Attribute
    /// 2. 使用 ProtocolRegistry 自动注册和编解码
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class ProtocolOpCodeAttribute : Attribute
    {
        /// <summary>
        /// 协议操作码
        /// </summary>
        public uint OpCode { get; }

        /// <summary>
        /// 传输方向
        /// </summary>
        public ProtocolDirection Direction { get; }

        /// <summary>
        /// 协议名称（可选，用于日志和调试）
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// 标记协议类型及其 OpCode
        /// </summary>
        /// <param name="opCode">协议操作码</param>
        /// <param name="direction">传输方向，默认为双向</param>
        /// <param name="name">协议名称（可选）</param>
        public ProtocolOpCodeAttribute(uint opCode, ProtocolDirection direction = ProtocolDirection.Bidirectional, string? name = null)
        {
            OpCode = opCode;
            Direction = direction;
            Name = name;
        }
    }

    /// <summary>
    /// Caches the protocol metadata declared by a message type.
    /// </summary>
    public static class ProtocolMessageDescriptor<T>
    {
        private static readonly ProtocolOpCodeAttribute? Attribute =
            typeof(T).GetTypeInfo().GetCustomAttribute<ProtocolOpCodeAttribute>();

        public static uint OpCode => Require().OpCode;

        public static ProtocolDirection Direction => Require().Direction;

        public static uint RequireOpCode(ProtocolDirection expectedDirection)
        {
            var attribute = Require();
            if (attribute.Direction != expectedDirection &&
                attribute.Direction != ProtocolDirection.Bidirectional)
            {
                throw new InvalidOperationException(
                    $"Protocol type {typeof(T).FullName} declares direction {attribute.Direction}, " +
                    $"but {expectedDirection} was required.");
            }

            return attribute.OpCode;
        }

        private static ProtocolOpCodeAttribute Require()
        {
            return Attribute ?? throw new InvalidOperationException(
                $"Protocol type {typeof(T).FullName} must declare {nameof(ProtocolOpCodeAttribute)}.");
        }
    }
}
