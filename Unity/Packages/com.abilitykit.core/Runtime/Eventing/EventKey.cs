using System;

namespace AbilityKit.Core.Eventing
{
    /// <summary>
    /// 使用标识种类、字符串或整数标识以及参数类型区分事件通道。
    /// </summary>
    /// <remarks>
    /// 字符串按序号比较。字符串键和整数键即使显示内容相同也不相等。
    /// 默认值不等同于显式创建的零值整数键；哈希码仅用于当前运行时集合，不用于持久化或协议。
    /// </remarks>
    public readonly struct EventKey : IEquatable<EventKey>
    {
        private readonly byte _kind;

        /// <summary>
        /// 取得字符串标识；整数键中为空串，默认结构值中可能是空引用。
        /// </summary>
        public readonly string StringId;

        /// <summary>
        /// 取得整数标识；字符串键及默认结构值中为零，需结合构造种类解释。
        /// </summary>
        public readonly int IntId;

        /// <summary>
        /// 取得参数类型；默认结构值中可能是空引用。
        /// </summary>
        public readonly Type ArgsType;

        /// <summary>
        /// 使用字符串标识创建事件键；字符串空引用会抛出参数异常，允许空字符串。
        /// </summary>
        /// <remarks>
        /// 参数类型不能为空引用，否则抛出参数异常。
        /// </remarks>
        public EventKey(string id, Type argsType)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));

            _kind = 1;
            StringId = id;
            IntId = default;
            ArgsType = argsType;
        }

        /// <summary>
        /// 使用整数标识创建事件键；字符串字段初始化为空串。
        /// </summary>
        /// <remarks>
        /// 参数类型不能为空引用，否则抛出参数异常。
        /// </remarks>
        public EventKey(int id, Type argsType)
        {
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));

            _kind = 2;
            StringId = string.Empty;
            IntId = id;
            ArgsType = argsType;
        }

        /// <summary>
        /// 按构造种类和标识比较事件键；字符串使用序号相等比较。
        /// </summary>
        /// <remarks>
        /// 参数类型也必须相同。
        /// </remarks>
        public bool Equals(EventKey other)
        {
            if (_kind != other._kind) return false;
            if (ArgsType != other.ArgsType) return false;
            if (_kind == 2) return IntId == other.IntId;
            return string.Equals(StringId, other.StringId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 仅当对象为相同事件键类型且各标识相等时返回真。
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is EventKey other && Equals(other);
        }

        /// <summary>
        /// 计算与键值相等规则一致的运行时哈希码；不承诺跨进程或跨版本稳定。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var idHash = _kind == 2 ? IntId : (StringId != null ? StringComparer.Ordinal.GetHashCode(StringId) : 0);
                return (((_kind * 397) ^ idHash) * 397) ^ (ArgsType != null ? ArgsType.GetHashCode() : 0);
            }
        }
    }
}
