using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 数值来源策略 — 框架层定义契约，业务层实现
    //
    // 设计原则：
    //  - 框架只定义"如何获取数值"的接口
    //  - 业务定义"数值从哪里来"的实现
    //  - 支持多种数值来源：固定值、等级曲线、属性引用、公式计算等
    // ============================================================================

    /// <summary>
    /// 数值来源策略ID
    /// </summary>
    public readonly struct MagnitudeStrategyId : IEquatable<MagnitudeStrategyId>
    {
        public readonly string Id;

        public MagnitudeStrategyId(string id)
        {
            Id = id ?? string.Empty;
        }

        public static MagnitudeStrategyId None => default;
        public bool IsValid => !string.IsNullOrEmpty(Id);

        public static bool operator ==(MagnitudeStrategyId a, MagnitudeStrategyId b) => a.Id == b.Id;
        public static bool operator !=(MagnitudeStrategyId a, MagnitudeStrategyId b) => a.Id != b.Id;
        public bool Equals(MagnitudeStrategyId other) => Id == other.Id;
        public override bool Equals(object obj) => obj is MagnitudeStrategyId other && Equals(other);
        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
        public override string ToString() => Id ?? "None";

        // 内置ID常量
        public static MagnitudeStrategyId Fixed => new("fixed");
        public static MagnitudeStrategyId Scalable => new("scalable");
        public static MagnitudeStrategyId Attribute => new("attribute");
        public static MagnitudeStrategyId Formula => new("formula");
    }

    /// <summary>
    /// 属性抓取类型（用于 Attribute 来源）
    /// </summary>
    public enum AttributeCaptureType : byte
    {
        /// <summary>使用属性当前值</summary>
        Current = 0,

        /// <summary>使用属性基础值（不含修改器）</summary>
        Base = 1,

        /// <summary>使用属性 Bonus 值（当前 - 基础）</summary>
        Bonus = 2,
    }

    /// <summary>
    /// 数值来源策略接口。
    /// 定义如何获取修改器的数值。
    ///
    /// 替代原有的 MagnitudeSource 枚举，支持业务层扩展：
    /// - 固定值
    /// - 等级曲线
    /// - 属性引用
    /// - 公式计算
    /// - 任何自定义逻辑
    /// </summary>
    public interface IMagnitudeStrategy
    {
        /// <summary>
        /// 策略唯一标识
        /// 框架内置：fixed、scalable、attribute、formula
        /// 业务层可自定义：custom_xxx
        /// </summary>
        MagnitudeStrategyId StrategyId { get; }

        /// <summary>
        /// 策略描述（用于调试）
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 计算数值
        /// </summary>
        /// <param name="level">当前等级</param>
        /// <param name="context">修改器上下文（用于获取属性等）</param>
        /// <returns>计算后的数值</returns>
        float Calculate(float level, IModifierContext context);

        /// <summary>
        /// 获取原始数值（用于显示调试信息，不考虑等级）
        /// </summary>
        float GetBaseValue();
    }

    // ============================================================================
    // 内置数值来源策略
    // ============================================================================

    /// <summary>
    /// 固定值策略
    /// </summary>
    [MagnitudeStrategyImpl("fixed")]
    public sealed class FixedMagnitudeStrategy : IMagnitudeStrategy
    {
        public MagnitudeStrategyId StrategyId => MagnitudeStrategyId.Fixed;
        public string Description => "Fixed value";

        /// <summary>固定数值</summary>
        public float Value { get; set; }

        public FixedMagnitudeStrategy() { }
        public FixedMagnitudeStrategy(float value) => Value = value;

        public float Calculate(float level, IModifierContext context) => Value;
        public float GetBaseValue() => Value;
    }

    /// <summary>
    /// 等级曲线策略
    /// </summary>
    [MagnitudeStrategyImpl("scalable")]
    public sealed class ScalableMagnitudeStrategy : IMagnitudeStrategy
    {
        public MagnitudeStrategyId StrategyId => MagnitudeStrategyId.Scalable;
        public string Description => "Level-based scaling";

        /// <summary>基础值</summary>
        public float BaseValue { get; set; }

        /// <summary>系数</summary>
        public float Coefficient { get; set; } = 1f;

        /// <summary>
        /// 缩放曲线数组。
        /// X 轴为等级，Y 轴为缩放系数。
        /// 格式：level1,curve1,level2,curve2,...
        /// </summary>
        public float[] Curve { get; set; }

        public ScalableMagnitudeStrategy() { }
        public ScalableMagnitudeStrategy(float baseValue, float[] curve = null, float coefficient = 1f)
        {
            BaseValue = baseValue;
            Curve = curve;
            Coefficient = coefficient;
        }

        public float Calculate(float level, IModifierContext context)
        {
            float multiplier = 1f;
            if (Curve != null && Curve.Length >= 2)
            {
                multiplier = InterpolateCurve(level);
            }
            return BaseValue * Coefficient * multiplier;
        }

        public float GetBaseValue() => BaseValue;

        private float InterpolateCurve(float level)
        {
            int count = Curve.Length / 2;
            if (count == 0) return 1f;
            if (count == 1) return Curve[1];

            for (int i = 0; i < count - 1; i++)
            {
                float level0 = Curve[i * 2];
                float value0 = Curve[i * 2 + 1];
                float level1 = Curve[(i + 1) * 2];
                float value1 = Curve[(i + 1) * 2 + 1];

                if (level <= level1)
                {
                    float t = (level - level0) / (level1 - level0);
                    return value0 + (value1 - value0) * t;
                }
            }

            return Curve[Curve.Length - 1];
        }
    }

    /// <summary>
    /// 属性引用策略
    /// </summary>
    [MagnitudeStrategyImpl("attribute")]
    public sealed class AttributeMagnitudeStrategy : IMagnitudeStrategy
    {
        public MagnitudeStrategyId StrategyId => MagnitudeStrategyId.Attribute;
        public string Description => "Attribute-based value";

        /// <summary>参考属性键</summary>
        public ModifierKey AttributeKey { get; set; }

        /// <summary>使用属性的哪个值</summary>
        public AttributeCaptureType CaptureType { get; set; } = AttributeCaptureType.Current;

        /// <summary>系数</summary>
        public float Coefficient { get; set; } = 1f;

        public AttributeMagnitudeStrategy() { }
        public AttributeMagnitudeStrategy(ModifierKey attributeKey, float coefficient = 1f, AttributeCaptureType captureType = AttributeCaptureType.Current)
        {
            AttributeKey = attributeKey;
            Coefficient = coefficient;
            CaptureType = captureType;
        }

        public float Calculate(float level, IModifierContext context)
        {
            if (context == null) return 0f;

            float attributeValue = context.GetAttribute(AttributeKey);
            return attributeValue * Coefficient;
        }

        public float GetBaseValue() => 0f;
    }

    /// <summary>
    /// 公式策略（业务层可扩展）
    /// </summary>
    [MagnitudeStrategyImpl("formula")]
    public sealed class FormulaMagnitudeStrategy : IMagnitudeStrategy
    {
        public MagnitudeStrategyId StrategyId => MagnitudeStrategyId.Formula;
        public string Description => "Custom formula";

        /// <summary>公式ID（业务层解释）</summary>
        public string FormulaId { get; set; }

        /// <summary>公式参数</summary>
        public float[] Parameters { get; set; }

        /// <summary>公式计算器（业务层实现）</summary>
        public IFormulaCalculator Calculator { get; set; }

        public FormulaMagnitudeStrategy() { }
        public FormulaMagnitudeStrategy(string formulaId, float[] parameters = null)
        {
            FormulaId = formulaId;
            Parameters = parameters;
        }

        public float Calculate(float level, IModifierContext context)
        {
            if (Calculator == null) return 0f;
            return Calculator.Calculate(FormulaId, level, context, Parameters);
        }

        public float GetBaseValue() => 0f;
    }

    /// <summary>
    /// 公式计算器接口
    /// </summary>
    public interface IFormulaCalculator
    {
        float Calculate(string formulaId, float level, IModifierContext context, float[] parameters);
    }

    // ============================================================================
    // 数值来源策略注册表
    // ============================================================================

    /// <summary>
    /// 数值来源策略注册表接口
    /// </summary>
    public interface IMagnitudeStrategyRegistry
    {
        /// <summary>
        /// 注册策略
        /// </summary>
        void Register(IMagnitudeStrategy strategy);

        /// <summary>
        /// 批量注册策略
        /// </summary>
        void RegisterRange(IEnumerable<IMagnitudeStrategy> strategies);

        /// <summary>
        /// 获取策略
        /// </summary>
        bool TryGet(MagnitudeStrategyId strategyId, out IMagnitudeStrategy strategy);
        bool TryGet(string strategyId, out IMagnitudeStrategy strategy);

        /// <summary>
        /// 获取所有已注册的策略
        /// </summary>
        IReadOnlyList<IMagnitudeStrategy> GetAll();

        /// <summary>
        /// 检查是否已注册
        /// </summary>
        bool IsRegistered(MagnitudeStrategyId strategyId);
    }

    /// <summary>
    /// 数值来源策略注册表默认实现
    /// </summary>
    public sealed class MagnitudeStrategyRegistry : IMagnitudeStrategyRegistry
    {
        private readonly Dictionary<string, IMagnitudeStrategy> _strategies = new();
        private readonly Dictionary<MagnitudeStrategyId, IMagnitudeStrategy> _strategiesById = new();
        private readonly object _lock = new();

        public void Register(IMagnitudeStrategy strategy)
        {
            if (strategy == null) return;

            lock (_lock)
            {
                _strategies[strategy.StrategyId.Id] = strategy;
                _strategiesById[strategy.StrategyId] = strategy;
            }
        }

        public void RegisterRange(IEnumerable<IMagnitudeStrategy> strategies)
        {
            if (strategies == null) return;

            lock (_lock)
            {
                foreach (var strategy in strategies)
                {
                    if (strategy != null)
                    {
                        _strategies[strategy.StrategyId.Id] = strategy;
                        _strategiesById[strategy.StrategyId] = strategy;
                    }
                }
            }
        }

        public bool TryGet(MagnitudeStrategyId strategyId, out IMagnitudeStrategy strategy)
        {
            lock (_lock)
            {
                return _strategiesById.TryGetValue(strategyId, out strategy);
            }
        }

        public bool TryGet(string strategyId, out IMagnitudeStrategy strategy)
        {
            if (string.IsNullOrEmpty(strategyId))
            {
                strategy = null;
                return false;
            }

            lock (_lock)
            {
                return _strategies.TryGetValue(strategyId, out strategy);
            }
        }

        public IReadOnlyList<IMagnitudeStrategy> GetAll()
        {
            lock (_lock)
            {
                return new List<IMagnitudeStrategy>(_strategies.Values);
            }
        }

        public bool IsRegistered(MagnitudeStrategyId strategyId)
        {
            lock (_lock)
            {
                return _strategiesById.ContainsKey(strategyId);
            }
        }

        /// <summary>
        /// 创建默认注册表（包含内置策略）
        /// </summary>
        public static IMagnitudeStrategyRegistry CreateDefault()
        {
            var registry = new MagnitudeStrategyRegistry();
            registry.RegisterRange(new IMagnitudeStrategy[]
            {
                new FixedMagnitudeStrategy(),
                new ScalableMagnitudeStrategy(),
                new AttributeMagnitudeStrategy(),
                new FormulaMagnitudeStrategy(),
            });
            return registry;
        }
    }

    // ============================================================================
    // 策略标记 Attribute
    // ============================================================================

    /// <summary>
    /// 标记数值来源策略实现的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MagnitudeStrategyImplAttribute : Attribute
    {
        /// <summary>
        /// 策略ID
        /// </summary>
        public string StrategyId { get; }

        public MagnitudeStrategyImplAttribute(string strategyId)
        {
            StrategyId = strategyId ?? throw new ArgumentNullException(nameof(strategyId));
        }
    }

    /// <summary>
    /// 数值来源策略扫描器
    /// </summary>
    public static class MagnitudeStrategyScanner
    {
        /// <summary>
        /// 扫描程序集并注册所有策略
        /// </summary>
        public static void ScanAndRegister(Assembly assembly, IMagnitudeStrategyRegistry registry)
        {
            if (assembly == null || registry == null) return;

            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<MagnitudeStrategyImplAttribute>();
                if (attr != null)
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var strategy = ctor.Invoke(null) as IMagnitudeStrategy;
                        if (strategy != null)
                        {
                            registry.Register(strategy);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 扫描调用程序集并注册所有策略
        /// </summary>
        public static void ScanAndRegister(IMagnitudeStrategyRegistry registry)
        {
            ScanAndRegister(Assembly.GetCallingAssembly(), registry);
        }
    }

    // ============================================================================
    // 扩展方法
    // ============================================================================

    /// <summary>
    /// 数值来源策略扩展方法
    /// </summary>
    public static class MagnitudeStrategyExtensions
    {
        /// <summary>
        /// 创建默认数值来源策略注册表
        /// </summary>
        public static IMagnitudeStrategyRegistry CreateDefaultRegistry()
        {
            return MagnitudeStrategyRegistry.CreateDefault();
        }

        /// <summary>
        /// 计算数值
        /// </summary>
        public static float CalculateMagnitude(
            this IMagnitudeStrategyRegistry registry,
            in MagnitudeStrategyData data,
            float level,
            IModifierContext context)
        {
            if (!string.IsNullOrEmpty(data.StrategyId))
            {
                if (registry.TryGet(data.StrategyId, out var strategy))
                {
                    return strategy.Calculate(level, context);
                }
            }

            // 降级：返回固定值
            return data.BaseValue;
        }
    }

    // ============================================================================
    // 数值来源策略数据（可序列化）
    // ============================================================================

    /// <summary>
    /// 数值来源策略数据。
    /// 用于存储在配置文件中。
    /// </summary>
    [Serializable]
    public struct MagnitudeStrategyData
    {
        /// <summary>
        /// 策略ID
        /// </summary>
        public string StrategyId;

        /// <summary>
        /// 是否有有效的策略ID
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(StrategyId);

        /// <summary>
        /// 基础值（用于 fixed 策略）
        /// </summary>
        public float BaseValue;

        /// <summary>
        /// 系数
        /// </summary>
        public float Coefficient;

        /// <summary>
        /// 等级曲线
        /// </summary>
        public float[] Curve;

        /// <summary>
        /// 属性键（用于 attribute 策略）
        /// </summary>
        public uint AttributeKeyPacked;

        /// <summary>
        /// 属性抓取类型
        /// </summary>
        public AttributeCaptureType CaptureType;

        #region 工厂方法

        /// <summary>
        /// 创建固定值策略数据
        /// </summary>
        public static MagnitudeStrategyData Fixed(float value)
            => new() { StrategyId = "fixed", BaseValue = value };

        /// <summary>
        /// 创建等级曲线策略数据
        /// </summary>
        public static MagnitudeStrategyData Scalable(float baseValue, float[] curve, float coefficient = 1f)
            => new() { StrategyId = "scalable", BaseValue = baseValue, Curve = curve, Coefficient = coefficient };

        /// <summary>
        /// 创建属性引用策略数据
        /// </summary>
        public static MagnitudeStrategyData Attribute(ModifierKey attributeKey, float coefficient = 1f, AttributeCaptureType captureType = AttributeCaptureType.Current)
            => new() { StrategyId = "attribute", AttributeKeyPacked = attributeKey.Packed, Coefficient = coefficient, CaptureType = captureType };

        /// <summary>
        /// 创建公式策略数据
        /// </summary>
        public static MagnitudeStrategyData Formula(string formulaId, float[] parameters)
            => new() { StrategyId = "formula" };

        #endregion

        #region 计算

        /// <summary>
        /// 计算数值（使用策略或降级到固定值）
        /// </summary>
        public float Calculate(float level, IModifierContext context)
        {
            if (string.IsNullOrEmpty(StrategyId))
            {
                return BaseValue;
            }

            var strategy = ToStrategy();
            if (strategy != null)
            {
                return strategy.Calculate(level, context);
            }

            return BaseValue;
        }

        #endregion

        #region 转换

        /// <summary>
        /// 转换为策略
        /// </summary>
        public IMagnitudeStrategy ToStrategy()
        {
            return StrategyId switch
            {
                "fixed" => new FixedMagnitudeStrategy(BaseValue),
                "scalable" => new ScalableMagnitudeStrategy(BaseValue, Curve, Coefficient),
                "attribute" => new AttributeMagnitudeStrategy(ModifierKey.FromPacked(AttributeKeyPacked), Coefficient, CaptureType),
                "formula" => new FormulaMagnitudeStrategy(),
                _ => null
            };
        }

        #endregion
    }
}
