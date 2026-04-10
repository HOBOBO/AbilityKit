using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 数值来源类型枚举（用于序列化）
    // ============================================================================

    /// <summary>
    /// 数值来源类型枚举
    /// </summary>
    public enum MagnitudeSourceType : byte
    {
        /// <summary>固定值</summary>
        Fixed = 0,

        /// <summary>等级曲线</summary>
        Scalable = 1,

        /// <summary>属性引用</summary>
        Attribute = 2,

        /// <summary>时间衰减</summary>
        TimeDecay = 3,

        /// <summary>修饰器管道</summary>
        Pipeline = 4,
    }

    // ============================================================================
    // 修改器上下文接口
    // ============================================================================

    /// <summary>
    /// 修改器上下文接口。
    /// 提供修改器计算过程中需要的外部数据。
    ///
    /// 扩展功能：
    /// - 通用数据槽（业务层可注入任意数据）
    /// - 时间相关（用于时变修改器）
    /// - 生命周期回调（用于 Buff 管理）
    /// </summary>
    public interface IModifierContext
    {
        /// <summary>获取属性值</summary>
        float GetAttribute(ModifierKey key);

        /// <summary>获取当前等级</summary>
        float Level { get; }

        /// <summary>当前时间（秒）</summary>
        float CurrentTime { get; }

        /// <summary>增量时间（秒）</summary>
        float DeltaTime { get; }

        /// <summary>距离修改器生效的已过时间（秒）</summary>
        float ElapsedTime { get; }

        /// <summary>获取数据（泛型）</summary>
        T GetData<T>(string key) where T : class;

        /// <summary>尝试获取数据</summary>
        bool TryGetData<T>(string key, out T value) where T : class;

        /// <summary>获取浮点数据</summary>
        float GetFloat(string key);

        /// <summary>尝试获取浮点数据</summary>
        bool TryGetFloat(string key, out float value);

        /// <summary>获取整型数据</summary>
        int GetInt(string key);

        /// <summary>尝试获取整型数据</summary>
        bool TryGetInt(string key, out int value);

        /// <summary>获取修改器元数据</summary>
        ModifierMetadata Metadata { get; }
    }

    // ============================================================================
    // 修改器元数据
    // ============================================================================

    /// <summary>
    /// 修改器元数据（零 GC 版本）。
    /// 用于调试、UI 显示、日志记录等。
    ///
    /// 设计原则：
    /// - 零 GC：所有字段均为值类型
    /// - 零分配：使用 int 索引代替 string，使用位掩码代替 string[]
    /// - 快速比较：所有字段可直接比较
    ///
    /// 业务层可通过 ModifierMetadataRegistry 注册字符串并获取索引。
    /// </summary>
    [Serializable]
    public struct ModifierMetadata
    {
        /// <summary>来源名称索引（-1 表示无名称）</summary>
        public short SourceNameIndex;

        /// <summary>标签位掩码（支持最多 32 个标签）</summary>
        public uint TagsMask;

        /// <summary>修改器创建时间</summary>
        public float CreatedTime;

        /// <summary>修改器拥有者标识</summary>
        public int OwnerId;

        /// <summary>是否为空</summary>
        public bool IsEmpty => SourceNameIndex < 0 && TagsMask == 0 && OwnerId == 0;

        /// <summary>创建空元数据</summary>
        public static ModifierMetadata Empty => new()
        {
            SourceNameIndex = -1,
            TagsMask = 0,
            CreatedTime = 0f,
            OwnerId = 0
        };

        /// <summary>
        /// 创建带来源的元数据
        /// </summary>
        /// <param name="sourceName">来源名称（会注册到字符串表）</param>
        /// <param name="ownerId">拥有者 ID</param>
        /// <param name="tags">标签（会注册到位掩码）</param>
        public static ModifierMetadata Create(string sourceName, int ownerId = 0, params string[] tags)
            => new()
            {
                SourceNameIndex = ModifierMetadataRegistry.RegisterName(sourceName),
                TagsMask = ModifierMetadataRegistry.RegisterTags(tags),
                CreatedTime = 0f,
                OwnerId = ownerId
            };

        /// <summary>
        /// 创建带来源索引的元数据（高性能版本）
        /// </summary>
        public static ModifierMetadata CreateByIndex(short sourceNameIndex, uint tagsMask, int ownerId = 0)
            => new()
            {
                SourceNameIndex = sourceNameIndex,
                TagsMask = tagsMask,
                CreatedTime = 0f,
                OwnerId = ownerId
            };

        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        public bool HasTag(int tagIndex) => tagIndex < 32 && (TagsMask & (1u << tagIndex)) != 0;

        /// <summary>
        /// 获取来源名称（从注册表查找）
        /// </summary>
        public string GetSourceName() => ModifierMetadataRegistry.GetName(SourceNameIndex);
    }

    /// <summary>
    /// 修改器元数据注册表。
    /// 管理字符串和标签的注册与查找。
    ///
    /// 使用方式：
    /// 1. 业务层初始化时注册常用字符串
    /// 2. ModifierMetadata 使用 int 索引存储
    /// 3. 调试时通过索引查找实际字符串
    /// </summary>
    public static class ModifierMetadataRegistry
    {
        private static readonly string[] _names = new string[256];
        private static readonly Dictionary<string, byte> _nameToIndex = new();
        private static byte _nameCount = 0;

        /// <summary>
        /// 注册名称，返回索引
        /// </summary>
        public static short RegisterName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            if (_nameToIndex.TryGetValue(name, out var existingIndex))
                return existingIndex;

            if (_nameCount >= 255)
                return -1;

            byte index = _nameCount++;
            _names[index] = name;
            _nameToIndex[name] = index;
            return (short)index;
        }

        /// <summary>
        /// 获取名称
        /// </summary>
        public static string GetName(short index)
        {
            if (index < 0 || index >= _nameCount)
                return string.Empty;
            return _names[index];
        }

        /// <summary>
        /// 注册标签，返回位掩码
        /// </summary>
        public static uint RegisterTags(params string[] tags)
        {
            uint mask = 0;
            foreach (var tag in tags)
            {
                int tagIndex = RegisterTag(tag);
                if (tagIndex >= 0 && tagIndex < 32)
                    mask |= (1u << tagIndex);
            }
            return mask;
        }

        private static int RegisterTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return -1;

            if (!_nameToIndex.TryGetValue(tag, out var index))
            {
                if (_nameCount >= 255)
                    return -1;

                index = _nameCount++;
                _names[index] = tag;
                _nameToIndex[tag] = index;
            }
            return index;
        }

        /// <summary>
        /// 注册常用标签（批量初始化，提升性能）
        /// </summary>
        public static void RegisterCommonTags(params string[] tags)
        {
            foreach (var tag in tags)
            {
                RegisterTag(tag);
            }
        }
    }

    // ============================================================================
    // 生命周期管理器接口
    // ============================================================================

    /// <summary>
    /// 修改器生命周期管理器接口。
    /// 管理修改器的添加、更新、移除等事件。
    /// </summary>
    public interface IModifierLifecycle
    {
        /// <summary>修改器被添加时调用</summary>
        void OnAdded(in ModifierData modifier, IModifierContext context);

        /// <summary>每帧更新时调用（用于时变修改器）</summary>
        float? OnUpdate(in ModifierData modifier, IModifierContext context);

        /// <summary>修改器被移除时调用</summary>
        void OnRemoved(in ModifierData modifier, IModifierContext context);

        /// <summary>修改器即将过期时调用</summary>
        void OnExpiring(in ModifierData modifier, IModifierContext context, float remainingTime);
    }

    /// <summary>
    /// 空生命周期管理器
    /// </summary>
    public struct NullModifierLifecycle : IModifierLifecycle
    {
        public static NullModifierLifecycle Default => default;

        public void OnAdded(in ModifierData modifier, IModifierContext context) { }
        public float? OnUpdate(in ModifierData modifier, IModifierContext context) => null;
        public void OnRemoved(in ModifierData modifier, IModifierContext context) { }
        public void OnExpiring(in ModifierData modifier, IModifierContext context, float remainingTime) { }
    }

    // ============================================================================
    // 修改器操作接口
    // ============================================================================

    /// <summary>
    /// 修改器操作接口。
    /// 定义如何将修改器应用到基础值上。
    /// </summary>
    public interface IModifierOperator
    {
        ModifierOp OpCode { get; }
        string Name { get; }
        float Apply(float baseValue, float modifierValue);
        float CalculateContribution(float baseValue, float modifierValue);
        int Priority { get; }
        bool IsTerminal { get; }
        bool IsAdditive { get; }
    }

    // ============================================================================
    // 内置操作实现
    // ============================================================================

    /// <summary>加法操作</summary>
    public readonly struct AddOperator : IModifierOperator
    {
        public ModifierOp OpCode => ModifierOp.Add;
        public string Name => "Add";
        public int Priority => 10;
        public bool IsTerminal => false;
        public bool IsAdditive => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Apply(float baseValue, float modifierValue) => baseValue + modifierValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateContribution(float baseValue, float modifierValue) => modifierValue;
    }

    /// <summary>乘法操作</summary>
    public readonly struct MulOperator : IModifierOperator
    {
        public ModifierOp OpCode => ModifierOp.Mul;
        public string Name => "Multiply";
        public int Priority => 20;
        public bool IsTerminal => false;
        public bool IsAdditive => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Apply(float baseValue, float modifierValue) => baseValue * modifierValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateContribution(float baseValue, float modifierValue) => baseValue * (modifierValue - 1f);
    }

    /// <summary>覆盖操作</summary>
    public readonly struct OverrideOperator : IModifierOperator
    {
        public ModifierOp OpCode => ModifierOp.Override;
        public string Name => "Override";
        public int Priority => 0;
        public bool IsTerminal => true;
        public bool IsAdditive => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Apply(float baseValue, float modifierValue) => modifierValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateContribution(float baseValue, float modifierValue) => modifierValue - baseValue;
    }

    /// <summary>百分比加成操作</summary>
    public readonly struct PercentAddOperator : IModifierOperator
    {
        public ModifierOp OpCode => ModifierOp.PercentAdd;
        public string Name => "PercentAdd";
        public int Priority => 15;
        public bool IsTerminal => false;
        public bool IsAdditive => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Apply(float baseValue, float modifierValue) => baseValue * (1f + modifierValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float CalculateContribution(float baseValue, float modifierValue) => baseValue * modifierValue;
    }

    // ============================================================================
    // 操作注册表
    // ============================================================================

    /// <summary>
    /// 修改器操作注册表。
    /// 管理所有内置和自定义操作。
    /// </summary>
    public static class ModifierOperatorRegistry
    {
        private static readonly System.Collections.Generic.Dictionary<ModifierOp, IModifierOperator> _operators = new();
        private static bool _initialized = false;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            Initialize();
        }

        private static void Initialize()
        {
            if (_initialized) return;

            RegisterInternal(new AddOperator());
            RegisterInternal(new MulOperator());
            RegisterInternal(new OverrideOperator());
            RegisterInternal(new PercentAddOperator());

            _initialized = true;
        }

        private static void RegisterInternal(IModifierOperator op)
        {
            _operators[op.OpCode] = op;
        }

        /// <summary>注册自定义操作</summary>
        public static void Register(IModifierOperator op)
        {
            if (op == null) return;
            _operators[op.OpCode] = op;
        }

        /// <summary>获取操作</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IModifierOperator Get(ModifierOp op)
        {
            EnsureInitialized();
            if (_operators.TryGetValue(op, out var result))
                return result;
            return null;
        }

        /// <summary>获取操作符号</summary>
        public static string GetSymbol(ModifierOp op)
        {
            return op switch
            {
                ModifierOp.Add => "+",
                ModifierOp.Mul => "×",
                ModifierOp.Override => "=",
                ModifierOp.PercentAdd => "+%",
                _ => "?"
            };
        }
    }

    // ============================================================================
    // 统一数值来源结构（支持修饰器组合）
    // ============================================================================

    /// <summary>
    /// 统一数值来源结构。
    /// 支持：固定值、等级曲线、属性引用、时间衰减、修饰器管道。
    ///
    /// 设计原则：
    /// - 零 GC：所有字段均为值类型（除 ArrayData）
    /// - 序列化友好：保持简单字段便于 Unity 序列化
    /// - 语义化：提供类型安全的访问属性
    ///
    /// 字段布局：
    /// - Data0: 基础值/初始值
    /// - Data1: 系数/持续时间/属性键
    /// - Data2: 衰减类型/曲线参数
    /// - ArrayData: 曲线数据
    ///
    /// 使用示例：
    /// ```csharp
    /// // 固定值
    /// var source = MagnitudeSource.Fixed(100f);
    ///
    /// // 时间衰减
    /// var source = MagnitudeSource.TimeDecay(50f, 5f, DecayType.Exponential);
    ///
    /// // 等级曲线
    /// var source = MagnitudeSource.LevelCurve(10f, curve);
    ///
    /// // 修饰器管道（可组合多个修饰器）
    /// var pipeline = ModifierPipeline.Create()
    ///     .ThenTimeDecay(50f, 5f, DecayType.Exponential)
    ///     .ThenLevelCurve(10f, curve)
    ///     .ThenAttributeRef(ModifierKey.Strength, 0.5f);
    /// var source = MagnitudeSource.Pipeline(pipeline);
    ///
    /// // 计算当前数值
    /// float currentValue = source.Calculate(level, context);
    /// ```
    /// </summary>
    [Serializable]
    public struct MagnitudeSource
    {
        #region 序列化字段

        /// <summary>来源类型</summary>
        public MagnitudeSourceType Type;

        /// <summary>基础值/初始值</summary>
        public float Data0;

        /// <summary>系数/持续时间/属性键</summary>
        public float Data1;

        /// <summary>衰减类型/曲线参数</summary>
        public float Data2;

        /// <summary>曲线数据</summary>
        public float[] ArrayData;

        /// <summary>修饰器管道数据</summary>
        public MagnitudePipelineData PipelineData;

        #endregion

        #region 类型安全属性

        /// <summary>获取基础值（语义化属性）</summary>
        public float BaseValue => Data0;

        /// <summary>获取系数</summary>
        public float Coefficient => Data1;

        /// <summary>获取持续时间（TimeDecay）</summary>
        public float Duration => Data1;

        /// <summary>获取衰减类型（TimeDecay）</summary>
        public DecayType DecayType => (DecayType)Data2;

        /// <summary>获取属性键（Attribute）</summary>
        public ModifierKey AttributeKey => ModifierKey.FromPacked((uint)Data1);

        /// <summary>获取曲线数据</summary>
        public float[] CurveData => ArrayData;

        #endregion

        #region 计算属性

        /// <summary>
        /// 是否是时变来源（值会随时间变化）
        /// </summary>
        public bool IsTimeVarying => Type == MagnitudeSourceType.TimeDecay || Type == MagnitudeSourceType.Pipeline;

        #endregion

        #region 计算方法

        /// <summary>
        /// 计算当前数值（根据来源类型）
        /// </summary>
        public float Calculate(float level = 1f, IModifierContext context = null)
        {
            return Type switch
            {
                MagnitudeSourceType.Fixed => CalculateFixed(),
                MagnitudeSourceType.Scalable => CalculateScalable(level),
                MagnitudeSourceType.Attribute => CalculateAttribute(context),
                MagnitudeSourceType.TimeDecay => CalculateTimeDecay(context),
                MagnitudeSourceType.Pipeline => CalculatePipeline(context),
                _ => Data0
            };
        }

        private float CalculateFixed() => Data0;

        private float CalculatePipeline(IModifierContext context)
        {
            if (PipelineData.IsEmpty)
                return Data0;

            return PipelineData.Calculate(context, Data0);
        }

        private float CalculateScalable(float level)
        {
            float multiplier = 1f;
            if (ArrayData != null && ArrayData.Length >= 2)
            {
                multiplier = MagnitudeModifierUtils.InterpolateCurve(level, ArrayData);
            }
            return Data0 * Data1 * multiplier;
        }

        private float CalculateAttribute(IModifierContext ctx)
        {
            if (ctx == null) return 0f;
            return ctx.GetAttribute(AttributeKey) * Data0;
        }

        private float CalculateTimeDecay(IModifierContext context)
        {
            if (context == null || Duration <= 0f) return Data0;

            float elapsed = context.ElapsedTime;
            float t = Math.Min(elapsed / Duration, 1f);

            if (t >= 1f) return 0f;

            float decayMultiplier = MagnitudeModifierUtils.CalculateDecay(t, DecayType);
            return Data0 * decayMultiplier;
        }

        #endregion

        #region 工厂方法

        /// <summary>创建固定值来源</summary>
        public static MagnitudeSource Fixed(float value)
            => new() { Type = MagnitudeSourceType.Fixed, Data0 = value };

        /// <summary>创建等级曲线来源</summary>
        public static MagnitudeSource LevelCurve(float baseValue, float[] curve = null, float coefficient = 1f)
            => new() { Type = MagnitudeSourceType.Scalable, Data0 = baseValue, Data1 = coefficient, ArrayData = curve };

        /// <summary>创建属性引用来源</summary>
        public static MagnitudeSource Attribute(ModifierKey attributeKey, float coefficient = 1f)
            => new() { Type = MagnitudeSourceType.Attribute, Data0 = coefficient, Data1 = attributeKey.Packed };

        /// <summary>创建时间衰减来源</summary>
        public static MagnitudeSource TimeDecay(float initialValue, float duration, DecayType decayType = DecayType.Linear)
            => new() { Type = MagnitudeSourceType.TimeDecay, Data0 = initialValue, Data1 = duration, Data2 = (int)decayType };

        /// <summary>创建时间衰减来源（带系数）</summary>
        public static MagnitudeSource TimeDecay(float initialValue, float duration, float coefficient, DecayType decayType = DecayType.Linear)
            => new() { Type = MagnitudeSourceType.TimeDecay, Data0 = initialValue * coefficient, Data1 = duration, Data2 = (int)decayType };

        /// <summary>创建时间衰减来源（自定义曲线）</summary>
        public static MagnitudeSource TimeDecay(float initialValue, float duration, float[] customDecayCurve)
            => new() { Type = MagnitudeSourceType.TimeDecay, Data0 = initialValue, Data1 = duration, Data2 = (int)DecayType.CustomCurve, ArrayData = customDecayCurve };

        /// <summary>创建修饰器管道来源（支持复杂组合）</summary>
        public static MagnitudeSource Pipeline(ModifierPipeline pipeline)
            => new() { Type = MagnitudeSourceType.Pipeline, Data0 = pipeline.GetBaseValue(), PipelineData = MagnitudePipelineData.Create(pipeline) };

        /// <summary>创建修饰器管道来源（带基础值）</summary>
        public static MagnitudeSource Pipeline(float baseValue, ModifierPipeline pipeline)
            => new() { Type = MagnitudeSourceType.Pipeline, Data0 = baseValue, PipelineData = MagnitudePipelineData.Create(pipeline) };

        #endregion

        #region 工具方法

        /// <summary>是否是空来源</summary>
        public bool IsEmpty => Type == MagnitudeSourceType.Fixed && Data0 == 0f;

        /// <summary>复制并设置基础值</summary>
        public MagnitudeSource WithBaseValue(float value)
            => new() { Type = Type, Data0 = value, Data1 = Data1, Data2 = Data2, ArrayData = ArrayData, PipelineData = PipelineData };

        /// <summary>复制并设置系数</summary>
        public MagnitudeSource WithCoefficient(float coefficient)
            => new() { Type = Type, Data0 = Data0, Data1 = coefficient, Data2 = Data2, ArrayData = ArrayData, PipelineData = PipelineData };

        #endregion
    }

    // ============================================================================
    // 修饰器管道数据（用于序列化）
    // ============================================================================

    /// <summary>
    /// 修饰器管道数据。
    /// 用于 MagnitudeSource 的序列化。
    /// 支持存储最多 4 个修饰器。
    /// </summary>
    [Serializable]
    public struct MagnitudePipelineData
    {
        /// <summary>修饰器数量</summary>
        public byte Count;

        /// <summary>修饰器0</summary>
        public SingleModifierData Modifier0;

        /// <summary>修饰器1</summary>
        public SingleModifierData Modifier1;

        /// <summary>修饰器2</summary>
        public SingleModifierData Modifier2;

        /// <summary>修饰器3</summary>
        public SingleModifierData Modifier3;

        /// <summary>是否为空</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>创建管道数据</summary>
        public static MagnitudePipelineData Create(ModifierPipeline pipeline)
        {
            if (pipeline.Count == 0)
                return default;

            int count = Math.Min((int)pipeline.Count, 4);
            var data = new MagnitudePipelineData { Count = (byte)count };

            for (int i = 0; i < count; i++)
            {
                switch (i)
                {
                    case 0: data.Modifier0 = SingleModifierData.Create(pipeline[0]); break;
                    case 1: data.Modifier1 = SingleModifierData.Create(pipeline[1]); break;
                    case 2: data.Modifier2 = SingleModifierData.Create(pipeline[2]); break;
                    case 3: data.Modifier3 = SingleModifierData.Create(pipeline[3]); break;
                }
            }

            return data;
        }

        /// <summary>
        /// 计算管道输出
        /// </summary>
        public float Calculate(IModifierContext context, float baseValue)
        {
            if (Count == 0) return baseValue;

            float result = baseValue;

            for (int i = 0; i < Count; i++)
            {
                switch (i)
                {
                    case 0: result = Modifier0.Modify(context, result); break;
                    case 1: result = Modifier1.Modify(context, result); break;
                    case 2: result = Modifier2.Modify(context, result); break;
                    case 3: result = Modifier3.Modify(context, result); break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 单个修饰器数据（用于序列化）
    /// </summary>
    [Serializable]
    public struct SingleModifierData
    {
        /// <summary>修饰器类型</summary>
        public byte TypeId;

        /// <summary>参数0</summary>
        public float Param0;

        /// <summary>参数1</summary>
        public float Param1;

        /// <summary>参数2</summary>
        public float Param2;

        /// <summary>参数3</summary>
        public float Param3;

        /// <summary>曲线数据</summary>
        public float[] Curve;

        /// <summary>
        /// 创建修饰器数据
        /// </summary>
        public static SingleModifierData Create(IMagnitudeModifier modifier)
        {
            var data = new SingleModifierData { TypeId = modifier.ModifierTypeId };

            switch (modifier)
            {
                case TimeDecayModifier td:
                    data.Param0 = td.InitialValue;
                    data.Param1 = td.Duration;
                    data.Param2 = (int)td.DecayType;
                    data.Param3 = td.Coefficient;
                    data.Curve = td.Curve;
                    break;

                case LevelCurveModifier lc:
                    data.Param0 = lc.BaseValue;
                    data.Curve = lc.Curve;
                    break;

                case AttributeRefModifier ar:
                    data.Param0 = ar.AttributeKey.Packed;
                    data.Param1 = ar.Coefficient;
                    break;

                case ScaleModifier sm:
                    data.Param0 = sm.Scale;
                    break;

                case FixedModifier:
                    break;
            }

            return data;
        }

        /// <summary>
        /// 创建修饰器实例
        /// </summary>
        public IMagnitudeModifier CreateModifier()
        {
            return TypeId switch
            {
                0 => new FixedModifier(),
                1 => Curve != null
                    ? new TimeDecayModifier(Param0, Param1, Curve)
                    : new TimeDecayModifier(Param0, Param1, (DecayType)Param2, Param3),
                2 => new LevelCurveModifier(Param0, Curve),
                3 => new AttributeRefModifier(ModifierKey.FromPacked((uint)Param0), Param1),
                4 => new ScaleModifier(Param0),
                _ => new FixedModifier()
            };
        }

        /// <summary>
        /// 修改数值
        /// </summary>
        public float Modify(IModifierContext context, float input)
        {
            var modifier = CreateModifier();
            return modifier.Modify(context, input);
        }
    }
}
