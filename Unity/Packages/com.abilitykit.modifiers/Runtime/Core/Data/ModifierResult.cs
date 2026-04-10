using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器计算结果（通用版本）
    // ============================================================================

    /// <summary>
    /// 修改器计算结果（通用版本）。
    /// 用于任何类型的值。
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
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

    // ============================================================================
    // 修改器计算结果（float 版本 - 零 GC）
    // ============================================================================

    /// <summary>
    /// 修改器计算结果（float 版本）。
    ///
    /// 计算公式（按优先级从高到低）：
    /// 1. Override → 直接使用覆盖值，忽略其他所有修改
    /// 2. PercentAdd → Base × PercentProduct
    /// 3. Mul → Base × MulProduct
    /// 4. Add → Base + AddSum
    ///
    /// 最终公式（无 Override）：
    /// FinalValue = (BaseValue + AddSum) × PercentProduct × MulProduct
    ///
    /// 设计原则：
    /// - 零 GC：所有字段均为值类型，无 Nullable<T> 装箱
    /// - 高性能：使用 byte flag 替代 float?，减少条件分支开销
    /// - 可预测：Override 优先级始终最高
    /// </summary>
    public struct ModifierResult
    {
        #region 核心字段

        /// <summary>基础值（未经任何修改的原始值）</summary>
        public float BaseValue;

        /// <summary>加法修改器之和</summary>
        public float AddSum;

        /// <summary>百分比加成修改器之积（每个元素为 1 + value）</summary>
        public float PercentProduct;

        /// <summary>乘法修改器之积</summary>
        public float MulProduct;

        /// <summary>覆盖值（如果有 Override 操作）</summary>
        public float OverrideValue;

        /// <summary>
        /// Override 标志。
        /// 0 = 无 Override，非 0 = 有 Override。
        /// 使用 byte 而非 bool 以避免对齐问题。
        /// </summary>
        public byte OverrideFlag;

        /// <summary>生效的修改器数量</summary>
        public int Count;

        #endregion

        #region 计算属性

        /// <summary>
        /// 计算后的最终值。
        /// 如果有 Override，优先返回覆盖值。
        /// 计算公式：FinalValue = (BaseValue + AddSum) × PercentProduct × MulProduct
        /// </summary>
        public float FinalValue => OverrideFlag != 0 
            ? OverrideValue 
            : (BaseValue + AddSum) * PercentProduct * MulProduct;

        /// <summary>是否有覆盖操作</summary>
        public bool HasOverride => OverrideFlag != 0;

        /// <summary>是否有生效的修改器</summary>
        public bool HasModifiers => Count > 0;

        #endregion

        #region 扩展信息

        /// <summary>
        /// 净变化值（FinalValue - BaseValue）
        /// </summary>
        public float NetChange => FinalValue - BaseValue;

        /// <summary>
        /// 获取相对于基础值的百分比变化
        /// </summary>
        public float PercentChange
        {
            get
            {
                if (MathF.Abs(BaseValue) < 0.0001f)
                    return FinalValue != 0f ? float.PositiveInfinity : 0f;
                return (FinalValue - BaseValue) / MathF.Abs(BaseValue);
            }
        }

        /// <summary>
        /// 百分比变化（返回百分比形式，如 20 表示 20%）
        /// </summary>
        public float PercentChange100 => PercentChange * 100f;

        #endregion

        #region 工厂方法

        /// <summary>创建空结果（只有基础值）</summary>
        public static ModifierResult Empty(float baseValue)
            => new()
            {
                BaseValue = baseValue,
                AddSum = 0f,
                PercentProduct = 1f,
                MulProduct = 1f,
                OverrideValue = 0f,
                OverrideFlag = 0,
                Count = 0
            };

        /// <summary>创建带 Override 的结果</summary>
        public static ModifierResult WithOverride(float baseValue, float overrideValue)
            => new()
            {
                BaseValue = baseValue,
                AddSum = 0f,
                PercentProduct = 1f,
                MulProduct = 1f,
                OverrideValue = overrideValue,
                OverrideFlag = 1,
                Count = 1
            };

        /// <summary>创建带加法和乘法的结果</summary>
        public static ModifierResult WithAddMul(float baseValue, float addSum, float percentProduct, float mulProduct, int count = 1)
            => new()
            {
                BaseValue = baseValue,
                AddSum = addSum,
                PercentProduct = percentProduct,
                MulProduct = mulProduct,
                OverrideValue = 0f,
                OverrideFlag = 0,
                Count = count
            };

        #endregion

        #region 操作方法

        /// <summary>
        /// 设置 Override 值
        /// </summary>
        public void SetOverride(float value)
        {
            OverrideValue = value;
            OverrideFlag = 1;
        }

        /// <summary>
        /// 添加加法值
        /// </summary>
        public void Add(float value)
        {
            AddSum += value;
            Count++;
        }

        /// <summary>
        /// 添加乘法值
        /// </summary>
        public void Mul(float value)
        {
            MulProduct *= value;
            Count++;
        }

        /// <summary>
        /// 合并另一个结果
        /// </summary>
        public ModifierResult Merge(in ModifierResult other)
        {
            if (other.OverrideFlag != 0)
            {
                return other;
            }

            return new ModifierResult
            {
                BaseValue = BaseValue,
                AddSum = AddSum + other.AddSum,
                PercentProduct = PercentProduct * other.PercentProduct,
                MulProduct = MulProduct * other.MulProduct,
                OverrideValue = 0f,
                OverrideFlag = 0,
                Count = Count + other.Count
            };
        }

        #endregion

        #region 调试

        public override string ToString()
        {
            if (HasOverride)
                return $"Override({OverrideValue:F2}) <- {BaseValue:F2} (Cnt={Count})";
            if (Count == 0)
                return $"Base({BaseValue:F2})";
            return $"({BaseValue:F2} + {AddSum:F2}) × {PercentProduct:F2} × {MulProduct:F2} = {FinalValue:F2}";
        }

        #endregion
    }

    // ============================================================================
    // 修改器来源条目
    // ============================================================================

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
        public short SourceNameIndex;

        /// <summary>
        /// 获取来源名称
        /// </summary>
        public string GetSourceName() => ModifierMetadataRegistry.GetName(SourceNameIndex);
    }

    // ============================================================================
    // 修改器来源记录器接口
    // ============================================================================

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
        public static NullRecorder Default => default;

        public void Begin(int capacityHint) { }
        public void Record(in ModifierSourceEntry entry) { }
        public int Count => 0;
        public ref readonly ModifierSourceEntry GetEntry(int index) => throw new IndexOutOfRangeException();
    }
}
