using System;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 修改器计算结果（通用版本）。
    /// 用于任何类型的值。
    /// </summary>
    public struct ModifierResult<T>
    {
        /// <summary>基础值（未经任何修改的原始值）</summary>
        public T BaseValue;

        /// <summary>计算后的最终值</summary>
        public T FinalValue;

        /// <summary>生效的修改器数量</summary>
        public int Count;

        /// <summary>是否有生效的修改器</summary>
        public bool HasModifiers => Count > 0;

        /// <summary>创建空结果</summary>
        public static ModifierResult<T> Empty(T baseValue)
            => new()
            {
                BaseValue = baseValue,
                FinalValue = baseValue,
                Count = 0
            };
    }

    /// <summary>
    /// 修改器计算结果（float 版本）。
    ///
    /// 计算公式（按优先级从高到低）：
    /// 1. Override → 直接使用覆盖值，忽略其他所有修改
    /// 2. Mul → Base × M1 × M2 × ...
    /// 3. Add → Base + A1 + A2 + ...
    ///
    /// 最终公式（无 Override）：
    /// FinalValue = (Base + Sum(Add)) × Product(Mul)
    ///
    /// 纯值类型，不产生 GC。
    /// </summary>
    public struct ModifierResult
    {
        /// <summary>基础值（未经任何修改的原始值）</summary>
        public float BaseValue;

        /// <summary>加法修改器之和</summary>
        public float AddSum;

        /// <summary>乘法修改器之积</summary>
        public float MulProduct;

        /// <summary>覆盖值（如果有 Override 操作）</summary>
        public float? OverrideValue;

        /// <summary>生效的修改器数量</summary>
        public int Count;

        /// <summary>计算后的最终值</summary>
        public float FinalValue => OverrideValue ?? (BaseValue + AddSum) * MulProduct;

        /// <summary>是否有覆盖操作</summary>
        public bool HasOverride => OverrideValue.HasValue;

        /// <summary>是否有生效的修改器</summary>
        public bool HasModifiers => Count > 0;

        /// <summary>获取相对于基础值的百分比变化</summary>
        public float PercentChange
        {
            get
            {
                if (BaseValue == 0f) return FinalValue != 0f ? float.PositiveInfinity : 0f;
                return (FinalValue - BaseValue) / BaseValue;
            }
        }

        #region 工厂方法

        /// <summary>创建空结果（只有基础值）</summary>
        public static ModifierResult Empty(float baseValue)
            => new()
            {
                BaseValue = baseValue,
                AddSum = 0f,
                MulProduct = 1f,
                Count = 0,
                OverrideValue = null
            };

        #endregion

        public override string ToString()
        {
            if (OverrideValue.HasValue)
                return $"Override({OverrideValue}) <- {BaseValue}";
            if (Count == 0)
                return $"Base({BaseValue})";
            return $"({BaseValue} + {AddSum}) × {MulProduct} = {FinalValue}";
        }
    }

    /// <summary>
    /// 修改器来源条目。
    /// 纯值类型，不含 string。
    /// </summary>
    public struct ModifierSourceEntry
    {
        /// <summary>操作类型</summary>
        public ModifierOp Op;

        /// <summary>原始数值</summary>
        public float Value;

        /// <summary>对最终值的贡献量</summary>
        public float Contribution;

        /// <summary>来源标识（业务层自行映射到名称）</summary>
        public int SourceId;

        /// <summary>来源名称索引（用于调试显示）</summary>
        public int SourceNameIndex;
    }

    /// <summary>
    /// 修改器来源记录器接口。
    /// 用于在计算过程中收集来源信息，零堆分配。
    /// </summary>
    public interface IModifierRecorder
    {
        /// <summary>开始记录</summary>
        void Begin(int capacityHint);

        /// <summary>记录一个来源</summary>
        void Record(in ModifierSourceEntry entry);

        /// <summary>获取已记录的来源数量</summary>
        int Count { get; }

        /// <summary>获取指定索引的来源</summary>
        ref readonly ModifierSourceEntry GetEntry(int index);
    }

    /// <summary>
    /// 默认来源记录器实现。
    /// 预分配固定大小数组，无扩容 GC。
    /// </summary>
    public struct DefaultRecorder : IModifierRecorder
    {
        private ModifierSourceEntry[] _entries;
        private int _count;

        public DefaultRecorder(int capacity = 8)
        {
            _entries = new ModifierSourceEntry[capacity];
            _count = 0;
        }

        public void Begin(int capacityHint)
        {
            if (capacityHint > _entries.Length)
                _entries = new ModifierSourceEntry[capacityHint];
            _count = 0;
        }

        public void Record(in ModifierSourceEntry entry)
        {
            if (_count < _entries.Length)
                _entries[_count] = entry;
            _count++;
        }

        public int Count => _count;

        public ref readonly ModifierSourceEntry GetEntry(int index)
            => ref _entries[index];
    }

    /// <summary>
    /// 空记录器（用于关闭来源追踪）
    /// </summary>
    public struct NullRecorder : IModifierRecorder
    {
        public void Begin(int capacityHint) { }
        public void Record(in ModifierSourceEntry entry) { }
        public int Count => 0;
        public ref readonly ModifierSourceEntry GetEntry(int index) => throw new IndexOutOfRangeException();
    }
}
