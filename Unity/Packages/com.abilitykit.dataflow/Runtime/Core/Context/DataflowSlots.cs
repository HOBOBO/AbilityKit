using System;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 数据流上下文数据槽位接口
    /// 用于定义上下文可以存储的数据类型
    /// </summary>
    public interface IDataflowSlot
    {
        /// <summary>
        /// 槽位名称（唯一标识）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 槽位类型
        /// </summary>
        Type ValueType { get; }
    }

    /// <summary>
    /// 强类型数据槽位
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public class DataflowSlot<T> : IDataflowSlot
    {
        public string Name { get; }
        public Type ValueType => typeof(T);

        private readonly Func<T> _defaultFactory;

        /// <summary>
        /// 创建数据槽位
        /// </summary>
        /// <param name="name">槽位名称（建议使用 PascalCase）</param>
        public DataflowSlot(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A non-empty slot name is required.", nameof(name));
            Name = name;
        }

        /// <summary>
        /// 创建带默认值的槽位
        /// </summary>
        public DataflowSlot(string name, T defaultValue)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A non-empty slot name is required.", nameof(name));
            Name = name;
            _defaultFactory = () => defaultValue;
        }

        /// <summary>
        /// 创建带默认工厂的槽位
        /// </summary>
        public DataflowSlot(string name, Func<T> defaultFactory)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A non-empty slot name is required.", nameof(name));
            Name = name;
            _defaultFactory = defaultFactory ?? throw new ArgumentNullException(nameof(defaultFactory));
        }

        /// <summary>
        /// 获取默认值
        /// </summary>
        public T GetDefault()
        {
            return _defaultFactory != null ? _defaultFactory() : default;
        }

        /// <summary>
        /// 隐式转换为槽位名称
        /// </summary>
        public static implicit operator string(DataflowSlot<T> slot)
        {
            if (slot == null) throw new ArgumentNullException(nameof(slot));
            return slot.Name;
        }

        public override string ToString() => Name;
    }

}
