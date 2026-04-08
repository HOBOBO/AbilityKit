using System;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器数据（重构版）
    // ============================================================================

    /// <summary>
    /// 修改器数据。
    /// 表示一个具体的、可生效的修改器实例。
    ///
    /// 重构说明：
    /// - 使用新的 MagnitudeSource 结构（支持时间衰减）
    /// - 保留原有 API 向后兼容
    /// - 新增 Metadata 字段用于调试
    ///
    /// 使用示例：
    /// ```csharp
    /// // 固定值修改
    /// var addMod = ModifierData.Add(ModifierKey.AttackPower, 100f);
    /// var mulMod = ModifierData.Mul(ModifierKey.AttackPower, 1.2f);
    ///
    /// // 时间衰减修改（新增）
    /// var speedBoost = ModifierData.Add(
    ///     ModifierKey.MoveSpeed,
    ///     MagnitudeSource.TimeDecay(50f, 5f, DecayType.Exponential)
    /// );
    /// ```
    /// </summary>
    [Serializable]
    public struct ModifierData : IEquatable<ModifierData>
    {
        #region 核心字段

        /// <summary>修改目标键</summary>
        public ModifierKey Key;

        /// <summary>操作类型</summary>
        public ModifierOp Op;

        /// <summary>优先级（数字越小越先计算）</summary>
        public int Priority;

        /// <summary>来源标识（业务层定义，用于溯源和批量移除）</summary>
        public int SourceId;

        /// <summary>来源名称索引（用于调试显示，-1 表示无名称）</summary>
        public short SourceNameIndex;

        #endregion

        #region 数值来源

        /// <summary>
        /// 数值来源策略数据
        /// 支持：固定值、等级曲线、属性引用、时间衰减
        /// </summary>
        public MagnitudeSource Magnitude;

        /// <summary>
        /// 获取当前生效的数值（根据数值来源计算）
        /// </summary>
        public float GetMagnitude(float level = 1f, IModifierContext context = null)
        {
            return Magnitude.Calculate(level, context);
        }

        #endregion

        #region 元数据

        /// <summary>
        /// 修改器元数据（用于调试/显示）
        /// </summary>
        public ModifierMetadata Metadata;

        #endregion

        #region 自定义数据

        /// <summary>
        /// 自定义数据槽（用于非数值类型的修改器）
        /// </summary>
        public CustomModifierData CustomData;

        #endregion

        #region 工厂方法 - 数值修改

        /// <summary>创建加法修改器（固定值）</summary>
        public static ModifierData Add(
            ModifierKey key,
            float value,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.Fixed(value),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建加法修改器（自定义数值来源）</summary>
        public static ModifierData Add(
            ModifierKey key,
            in MagnitudeSource magnitude,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Magnitude = magnitude,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建乘法修改器（固定值）</summary>
        public static ModifierData Mul(
            ModifierKey key,
            float value,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Magnitude = MagnitudeSource.Fixed(value),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建乘法修改器（自定义数值来源）</summary>
        public static ModifierData Mul(
            ModifierKey key,
            in MagnitudeSource magnitude,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Mul,
                Magnitude = magnitude,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建覆盖修改器（固定值）</summary>
        public static ModifierData Override(
            ModifierKey key,
            float value,
            int sourceId = 0,
            short sourceNameIndex = -1)
            => new()
            {
                Key = key,
                Op = ModifierOp.Override,
                Magnitude = MagnitudeSource.Fixed(value),
                Priority = 0,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建覆盖修改器（自定义数值来源）</summary>
        public static ModifierData Override(
            ModifierKey key,
            in MagnitudeSource magnitude,
            int sourceId = 0,
            short sourceNameIndex = -1)
            => new()
            {
                Key = key,
                Op = ModifierOp.Override,
                Magnitude = magnitude,
                Priority = 0,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>创建百分比加成修改器</summary>
        public static ModifierData PercentAdd(
            ModifierKey key,
            float percentValue,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.PercentAdd,
                Magnitude = MagnitudeSource.Fixed(percentValue),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        #endregion

        #region 工厂方法 - 时间衰减（新增）

        /// <summary>
        /// 创建带时间衰减的加法修改器。
        /// 数值随时间从初始值逐渐衰减到 0。
        /// </summary>
        /// <param name="key">修改目标键</param>
        /// <param name="initialValue">初始加成值</param>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="decayType">衰减类型</param>
        /// <param name="decayCoefficient">衰减系数（用于指数衰减）</param>
        /// <param name="sourceId">来源 ID</param>
        /// <param name="sourceNameIndex">来源名称索引</param>
        /// <param name="priority">优先级</param>
        public static ModifierData AddWithTimeDecay(
            ModifierKey key,
            float initialValue,
            float duration,
            DecayType decayType = DecayType.Exponential,
            float decayCoefficient = 2f,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.TimeDecay(initialValue, duration, decayCoefficient, decayType),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Create($"TimeDecay_{decayType}", sourceId)
            };

        /// <summary>
        /// 创建带时间衰减的百分比加成修改器。
        /// </summary>
        public static ModifierData PercentAddWithTimeDecay(
            ModifierKey key,
            float initialPercent,
            float duration,
            DecayType decayType = DecayType.Exponential,
            float decayCoefficient = 2f,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = ModifierOp.PercentAdd,
                Magnitude = MagnitudeSource.TimeDecay(initialPercent, duration, decayCoefficient, decayType),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Create($"TimeDecay_{decayType}", sourceId)
            };

        #endregion

        #region 工厂方法 - 修饰器管道（支持复杂组合）

        /// <summary>
        /// 创建带修饰器管道的修改器。
        /// 支持复杂的数值变换组合，如：时间衰减 + 等级曲线 + 属性引用。
        ///
        /// 使用示例：
        /// ```csharp
        /// var pipeline = ModifierPipeline.Create()
        ///     .ThenTimeDecay(50f, 5f, DecayType.Exponential)
        ///     .ThenLevelCurve(10f, levelCurve)
        ///     .ThenAttributeRef(ModifierKey.Strength, 0.5 f);
        ///
        /// var mod = ModifierData.CreateWithPipeline(
        ///     ModifierKey.AttackPower,
        ///     ModifierOp.Add,
        ///     pipeline,
        ///     sourceId: 1
        /// );
        /// ```
        /// </summary>
        public static ModifierData CreateWithPipeline(
            ModifierKey key,
            ModifierOp op,
            ModifierPipeline pipeline,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                Magnitude = MagnitudeSource.Pipeline(pipeline),
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Create("Pipeline", sourceId)
            };

        /// <summary>
        /// 创建带修饰器管道的加法修改器。
        /// </summary>
        public static ModifierData AddWithPipeline(
            ModifierKey key,
            ModifierPipeline pipeline,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => CreateWithPipeline(key, ModifierOp.Add, pipeline, sourceId, sourceNameIndex, priority);

        /// <summary>
        /// 创建带修饰器管道的乘法修改器。
        /// </summary>
        public static ModifierData MulWithPipeline(
            ModifierKey key,
            ModifierPipeline pipeline,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => CreateWithPipeline(key, ModifierOp.Mul, pipeline, sourceId, sourceNameIndex, priority);

        /// <summary>
        /// 创建带修饰器管道的百分比加成修改器。
        /// </summary>
        public static ModifierData PercentAddWithPipeline(
            ModifierKey key,
            ModifierPipeline pipeline,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => CreateWithPipeline(key, ModifierOp.PercentAdd, pipeline, sourceId, sourceNameIndex, priority);

        #endregion

        #region 工厂方法 - 通用

        /// <summary>
        /// 创建自定义修改器（使用 CustomData）
        /// </summary>
        public static ModifierData Custom(
            ModifierKey key,
            ModifierOp op,
            CustomModifierData customData,
            int sourceId = 0,
            short sourceNameIndex = -1,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                CustomData = customData,
                Priority = priority,
                SourceId = sourceId,
                SourceNameIndex = sourceNameIndex,
                Metadata = ModifierMetadata.Empty
            };

        /// <summary>
        /// 创建带数值的自定义修改器
        /// </summary>
        public static ModifierData CustomWithValue(
            ModifierKey key,
            ModifierOp op,
            float value,
            CustomModifierData customData,
            int sourceId = 0,
            int priority = 10)
            => new()
            {
                Key = key,
                Op = op,
                Magnitude = MagnitudeSource.Fixed(value),
                CustomData = customData,
                Priority = priority,
                SourceId = sourceId,
                Metadata = ModifierMetadata.Empty
            };

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
}