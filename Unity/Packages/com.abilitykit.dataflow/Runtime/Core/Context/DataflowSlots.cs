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
        private readonly T _defaultValue;

        /// <summary>
        /// 创建数据槽位
        /// </summary>
        /// <param name="name">槽位名称（建议使用 PascalCase）</param>
        public DataflowSlot(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// 创建带默认值的槽位
        /// </summary>
        public DataflowSlot(string name, T defaultValue)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _defaultValue = defaultValue;
            _defaultFactory = () => defaultValue;
        }

        /// <summary>
        /// 创建带默认工厂的槽位
        /// </summary>
        public DataflowSlot(string name, Func<T> defaultFactory)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
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
        public static implicit operator string(DataflowSlot<T> slot) => slot.Name;

        public override string ToString() => Name;
    }

    /// <summary>
    /// 预定义的通用数据槽位
    /// </summary>
    public static class DataflowSlots
    {
        /// <summary>
        /// 伤害计算上下文相关槽位
        /// </summary>
        public static class Damage
        {
            public static readonly DataflowSlot<float> CritChance = new DataflowSlot<float>("CritChance");
            public static readonly DataflowSlot<float> CritMultiplier = new DataflowSlot<float>("CritMultiplier", 1.5f);
            public static readonly DataflowSlot<float> DamageBonusPercent = new DataflowSlot<float>("DamageBonusPercent");
            public static readonly DataflowSlot<float> DamageBonusFlat = new DataflowSlot<float>("DamageBonusFlat");
            public static readonly DataflowSlot<float> ArmorPenetration = new DataflowSlot<float>("ArmorPenetration");
            public static readonly DataflowSlot<float> ArmorPenetrationPercent = new DataflowSlot<float>("ArmorPenetrationPercent");
            public static readonly DataflowSlot<float> MagicResistPenetration = new DataflowSlot<float>("MagicResistPenetration");
            public static readonly DataflowSlot<float> MagicResistPenetrationPercent = new DataflowSlot<float>("MagicResistPenetrationPercent");
            public static readonly DataflowSlot<float> TargetShield = new DataflowSlot<float>("TargetShield");
        }

        /// <summary>
        /// 治疗计算上下文相关槽位
        /// </summary>
        public static class Heal
        {
            public static readonly DataflowSlot<float> HealBonusPercent = new DataflowSlot<float>("HealBonusPercent");
            public static readonly DataflowSlot<float> HealBonusFlat = new DataflowSlot<float>("HealBonusFlat");
        }

        /// <summary>
        /// 通用槽位
        /// </summary>
        public static class Common
        {
            /// <summary>
            /// 当前执行的处理器索引
            /// </summary>
            public static readonly DataflowSlot<int> ProcessorIndex = new DataflowSlot<int>("ProcessorIndex", -1);

            /// <summary>
            /// 执行时间戳
            /// </summary>
            public static readonly DataflowSlot<double> Timestamp = new DataflowSlot<double>("Timestamp");
        }
    }
}