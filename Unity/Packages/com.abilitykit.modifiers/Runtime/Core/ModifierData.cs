using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器数据
    // ============================================================================

    /// <summary>
    /// 修改器数据。
    /// 表示一个具体的、可生效的修改器实例。
    ///
    /// 设计：
    /// - 纯值类型，无堆分配
    /// - 支持策略模式，支持业务层扩展
    /// - 数值来源通过 MagnitudeStrategyData 配置
    /// - 通用修改通过 StrategyData 配置
    /// </summary>
    [Serializable]
    public struct ModifierData : IEquatable<ModifierData>
    {
        #region 核心字段

        /// <summary>修改目标键</summary>
        public ModifierKey Key;

        /// <summary>操作类型</summary>
        public ModifierOp Op;

        /// <summary>优先级。数字越小越先计算。同一标签下按优先级排序。</summary>
        public int Priority;

        /// <summary>来源标识（业务层定义，用于溯源和批量移除）</summary>
        public int SourceId;

        /// <summary>来源名称索引（用于调试显示，业务层自行映射）</summary>
        public int SourceNameIndex;

        /// <summary>
        /// 叠加配置（可选）
        /// </summary>
        public StackingConfig? Stacking;

        /// <summary>
        /// 自定义数据槽（用于扩展类型的修改器）
        /// 例如：技能 ID、Prefab 引用、配置结构等
        /// </summary>
        public CustomModifierData CustomData;

        #endregion

        #region 数值来源策略

        /// <summary>
        /// 数值来源策略数据
        /// 支持业务层扩展：固定值、等级曲线、属性引用、公式计算等
        /// </summary>
        public MagnitudeStrategyData Magnitude;

        /// <summary>
        /// 是否有数值来源策略数据
        /// </summary>
        public bool HasMagnitude => !string.IsNullOrEmpty(Magnitude.StrategyId);

        #endregion

        #region 策略数据

        /// <summary>
        /// 策略数据（用于通用策略模式）
        /// 支持状态修改、标签管理、列表操作等非数值类修改
        /// </summary>
        public StrategyData Strategy;

        /// <summary>
        /// 是否有策略数据
        /// </summary>
        public bool HasStrategy => !string.IsNullOrEmpty(Strategy.StrategyId);

        #endregion

        #region 工厂方法

        /// <summary>创建加法修改器（固定值）</summary>
        public static ModifierData Add(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Magnitude = MagnitudeStrategyData.Fixed(value),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建乘法修改器（固定值）</summary>
        public static ModifierData Mul(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Magnitude = MagnitudeStrategyData.Fixed(value),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建覆盖修改器（固定值）</summary>
        public static ModifierData Override(
            ModifierKey key,
            float value,
            int sourceId = 0,
            int sourceNameIndex = 0)
            => new()
            {
                Key = key,
                Op = ModifierOp.Override,
                Magnitude = MagnitudeStrategyData.Fixed(value),
                Priority = 0,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建百分比加成修改器</summary>
        public static ModifierData PercentAdd(
            ModifierKey key,
            float percentValue,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.PercentAdd,
                Magnitude = MagnitudeStrategyData.Fixed(percentValue),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        /// <summary>创建自定义修改器（使用 CustomData）</summary>
        public static ModifierData Custom(
            ModifierKey key,
            ModifierOp op,
            CustomModifierData customData,
            int sourceId = 0,
            int sourceNameIndex = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                CustomData = customData,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex
            };

        #endregion

        #region 策略工厂方法

        /// <summary>
        /// 创建数值来源策略修改器
        /// </summary>
        public static ModifierData MagnitudeStrategy(
            ModifierKey key,
            ModifierOp op,
            MagnitudeStrategyData magnitude,
            int sourceId = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                Magnitude = magnitude,
                Priority = priority,
                SourceId = sourceId
            };

        /// <summary>
        /// 创建状态策略修改器
        /// </summary>
        public static ModifierData State(
            ModifierKey key,
            string stateKey,
            object value,
            long ownerKey,
            int sourceId = 0,
            int priority = 0)
            => new()
            {
                Key = key,
                Op = ModifierOp.Custom,
                Strategy = StrategyData.State(
                    "state.set",
                    StrategyOperationKind.SaveAndSet,
                    stateKey,
                    value,
                    ownerKey,
                    priority,
                    sourceId
                ),
                Priority = priority,
                SourceId = sourceId
            };

        /// <summary>
        /// 创建标签策略修改器
        /// </summary>
        public static ModifierData Tag(
            ModifierKey key,
            string tag,
            bool isAdd,
            long ownerKey,
            int sourceId = 0)
            => new()
            {
                Key = key,
                Op = ModifierOp.Custom,
                Strategy = StrategyData.Tag(
                    isAdd ? "tag.add" : "tag.remove",
                    isAdd ? StrategyOperationKind.ListAdd : StrategyOperationKind.ListRemove,
                    tag,
                    ownerKey,
                    sourceId
                ),
                Priority = 0,
                SourceId = sourceId
            };

        /// <summary>
        /// 创建通用策略修改器
        /// </summary>
        public static ModifierData Create(
            ModifierKey key,
            StrategyData strategy,
            int sourceId = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Custom,
                Strategy = strategy,
                Priority = priority,
                SourceId = sourceId
            };

        #endregion

        #region 计算

        /// <summary>
        /// 获取当前生效的数值（根据数值来源策略计算）
        /// </summary>
        public float GetMagnitude(float level = 1f, IModifierContext context = null)
        {
            if (HasMagnitude)
            {
                return Magnitude.Calculate(level, context);
            }
            return 0f;
        }

        #endregion

        #region IEquatable

        public bool Equals(ModifierData other)
            => Key == other.Key
            && Op == other.Op
            && Priority == other.Priority
            && SourceId == other.SourceId;

        public override bool Equals(object obj) => obj is ModifierData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Key, Op, Priority, SourceId);

        #endregion

        public override string ToString()
            => $"ModifierData({Key}, {Op} {Magnitude.BaseValue}, Prio={Priority}, Src={SourceId})";
    }

    // ============================================================================
    // 自定义修改器数据
    // ============================================================================

    /// <summary>
    /// 自定义修改器数据。
    /// 用于存储非数值类型的修改器数据。
    /// </summary>
    [Serializable]
    public struct CustomModifierData : IEquatable<CustomModifierData>
    {
        /// <summary>自定义类型 ID（业务层定义）</summary>
        public int CustomTypeId;

        /// <summary>整数数据</summary>
        public int IntValue;

        /// <summary>字符串数据</summary>
        public string StringValue;

        /// <summary>原始字节数据（用于序列化复杂结构）</summary>
        public byte[] RawData;

        #region 工厂方法

        /// <summary>创建技能 ID 修改器</summary>
        public static CustomModifierData SkillId(int skillId)
            => new() { CustomTypeId = 1, IntValue = skillId };

        /// <summary>创建字符串数据</summary>
        public static CustomModifierData String(string value)
            => new() { CustomTypeId = 2, StringValue = value };

        #endregion

        public bool Equals(CustomModifierData other)
            => CustomTypeId == other.CustomTypeId && IntValue == other.IntValue;

        public override bool Equals(object obj) => obj is CustomModifierData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CustomTypeId, IntValue);
    }
}
