using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Modifiers
{
    /// <summary>
    /// 声明式自定义修改器操作。
    /// 标记一个静态方法为自定义修改器操作处理器。
    /// 
    /// 使用方式：
    /// <code>
    /// public class MyExtensions
    /// {
    ///     [ModifierOperation("MyCustomOp")]
    ///     public static float HandleMyOp(float baseValue, in ModifierData modifier, IModifierContext ctx)
    ///     {
    ///         return baseValue * modifier.Value;
    ///     }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ModifierOperationAttribute : Attribute
    {
        /// <summary>操作名称（与 ModifierOp.Custom 配合使用）</summary>
        public string OperationName { get; }

        /// <summary>优先级（数字越大优先级越高）</summary>
        public int Priority { get; set; }

        public ModifierOperationAttribute(string operationName)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        }
    }

    /// <summary>
    /// 声明式属性修改器 Magnitude 来源。
    /// 用于自动将字段标记为基于属性的 Magnitude 来源。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AttributeMagnitudeAttribute : Attribute
    {
        /// <summary>关联的属性 Key</summary>
        public string AttributeKey { get; }

        /// <summary>捕获类型</summary>
        public AttributeCaptureType CaptureType { get; set; } = AttributeCaptureType.Current;

        /// <summary>系数</summary>
        public float Coefficient { get; set; } = 1f;

        public AttributeMagnitudeAttribute(string attributeKey)
        {
            AttributeKey = attributeKey ?? throw new ArgumentNullException(nameof(attributeKey));
        }

        /// <summary>
        /// 转换为 AttributeBasedMagnitude
        /// </summary>
        public AttributeBasedMagnitude ToMagnitude()
        {
            return new AttributeBasedMagnitude
            {
                AttributeKey = ModifierKey.Create(0, 0, GetKeyHash(AttributeKey)),
                CaptureType = CaptureType,
                Coefficient = Coefficient
            };
        }

        private static int GetKeyHash(string key)
        {
            // 简单的字符串哈希
            int hash = 17;
            for (int i = 0; i < key.Length; i++)
            {
                hash = hash * 31 + key[i];
            }
            return hash & 0xFFFF;
        }
    }

    /// <summary>
    /// 声明式可缩放 Magnitude 来源。
    /// 用于标记一个字段由等级曲线驱动。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ScalableMagnitudeAttribute : Attribute
    {
        /// <summary>基础值</summary>
        public float BaseValue { get; }

        /// <summary>
        /// 缩放曲线数组。
        /// 格式：level1,curve1,level2,curve2,...
        /// 例如：1,0.5,5,1.0,10,1.5 表示 1 级 0.5x，5 级 1.0x，10 级 1.5x
        /// </summary>
        public float[] LevelCurve { get; set; }

        /// <summary>系数</summary>
        public float Coefficient { get; set; } = 1f;

        public ScalableMagnitudeAttribute(float baseValue = 0f)
        {
            BaseValue = baseValue;
        }

        /// <summary>
        /// 转换为 ScalableFloat
        /// </summary>
        public ScalableFloat ToScalableFloat()
        {
            return new ScalableFloat
            {
                BaseValue = BaseValue,
                Coefficient = Coefficient,
                Curve = LevelCurve
            };
        }
    }

    /// <summary>
    /// 声明式修改器堆叠规则。
    /// 标记类型使用特定的堆叠行为。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class ModifierStackingAttribute : Attribute
    {
        /// <summary>堆叠行为</summary>
        public StackingType StackType { get; }

        /// <summary>堆叠标签（用于标识同类效果）</summary>
        public string StackTag { get; set; }

        /// <summary>最大堆叠数量</summary>
        public int MaxStackCount { get; set; } = 1;

        public ModifierStackingAttribute(StackingType stackType)
        {
            StackType = stackType;
        }

        /// <summary>
        /// 转换为 StackingConfig
        /// </summary>
        public StackingConfig? ToStackingConfig()
        {
            if (StackType == StackingType.Exclusive && MaxStackCount <= 1 && string.IsNullOrEmpty(StackTag))
                return null;

            return new StackingConfig
            {
                Type = StackType,
                StackKey = string.IsNullOrEmpty(StackTag) ? default : ModifierKey.Create(0, 0, GetKeyHash(StackTag)),
                MaxStackCount = MaxStackCount
            };
        }

        private static int GetKeyHash(string key)
        {
            int hash = 17;
            for (int i = 0; i < key.Length; i++)
            {
                hash = hash * 31 + key[i];
            }
            return hash & 0xFFFF;
        }
    }

    /// <summary>
    /// 声明式修改器来源追踪。
    /// 标记字段作为修改器的来源标识。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ModifierSourceAttribute : Attribute
    {
        /// <summary>来源名称</summary>
        public string SourceName { get; }

        /// <summary>是否在结果中记录来源</summary>
        public bool RecordInResult { get; set; } = true;

        public ModifierSourceAttribute(string sourceName)
        {
            SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
        }
    }

    /// <summary>
    /// 基于 Attribute 的修改器操作处理器。
    /// 自动扫描程序集，发现所有标记了 [ModifierOperation] 的方法并注册。
    /// </summary>
    public sealed class AttributedModifierHandler : NumericModifierHandler
    {
        private static readonly Dictionary<string, CustomOperationDelegate> _operations = new();
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 自定义操作委托签名
        /// </summary>
        public delegate float CustomOperationDelegate(float baseValue, in ModifierData modifier, IModifierContext context);

        /// <summary>
        /// 初始化：扫描程序集并注册所有声明式操作
        /// </summary>
        public static void Initialize()
        {
            Initialize(Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// 初始化：扫描指定程序集
        /// </summary>
        public static void Initialize(Assembly assembly)
        {
            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;

                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        var attr = method.GetCustomAttribute<ModifierOperationAttribute>();
                        if (attr != null)
                        {
                            // 验证方法签名
                            var parameters = method.GetParameters();
                            if (parameters.Length == 3 &&
                                parameters[0].ParameterType == typeof(float) &&
                                parameters[1].ParameterType.IsByRef &&
                                parameters[2].ParameterType == typeof(IModifierContext))
                            {
                                var del = (CustomOperationDelegate)Delegate.CreateDelegate(
                                    typeof(CustomOperationDelegate), method, false);

                                if (del != null)
                                {
                                    _operations[attr.OperationName] = del;
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 注册自定义操作
        /// </summary>
        public static void Register(string operationName, CustomOperationDelegate handler)
        {
            lock (_lock)
            {
                _operations[operationName] = handler;
            }
        }

        /// <summary>
        /// 获取已注册的操作数量
        /// </summary>
        public static int GetRegisteredCount()
        {
            lock (_lock)
            {
                return _operations.Count;
            }
        }

        /// <summary>
        /// 获取所有已注册的操作名称
        /// </summary>
        public static IEnumerable<string> GetRegisteredOperations()
        {
            lock (_lock)
            {
                return new List<string>(_operations.Keys);
            }
        }

        /// <inheritdoc/>
        protected override float ApplyCustom(float baseValue, in ModifierData modifier, IModifierContext context)
        {
            if (modifier.CustomData.CustomTypeId == 0)
            {
                // 使用字符串名称查找
                var name = modifier.CustomData.StringValue;
                if (!string.IsNullOrEmpty(name) && _operations.TryGetValue(name, out var handler))
                {
                    return handler(baseValue, modifier, context);
                }
            }

            return base.ApplyCustom(baseValue, modifier, context);
        }
    }

    /// <summary>
    /// Attribute 扫描器 - 用于扫描类型上的 Attribute 标记
    /// </summary>
    public static class ModifierAttributeScanner
    {
        /// <summary>
        /// 从类型上提取声明式的 Magnitude 配置
        /// </summary>
        public static MagnitudeType GetMagnitudeType(object instance)
        {
            if (instance == null) return MagnitudeType.None;

            var type = instance.GetType();

            // 检查 ScalableMagnitude
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<ScalableMagnitudeAttribute>();
                if (attr != null)
                {
                    return MagnitudeType.ScalableFloat;
                }
            }

            // 检查 AttributeMagnitude
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<AttributeMagnitudeAttribute>();
                if (attr != null)
                {
                    return MagnitudeType.AttributeBased;
                }
            }

            return MagnitudeType.None;
        }

        /// <summary>
        /// 从类型实例提取 Stacking 配置
        /// </summary>
        public static StackingConfig? GetStackingConfig(object instance)
        {
            if (instance == null) return null;

            var type = instance.GetType();
            var attr = type.GetCustomAttribute<ModifierStackingAttribute>();
            return attr?.ToStackingConfig();
        }

        /// <summary>
        /// 创建带声明式配置的 ModifierData
        /// </summary>
        public static ModifierData CreateFromInstance(object instance, ModifierKey key, ModifierOp op, float value)
        {
            var data = new ModifierData
            {
                Key = key,
                Op = op,
                Value = value,
                MagnitudeSource = GetMagnitudeType(instance)
            };

            // 填充 Magnitude 数据
            if (instance != null)
            {
                var type = instance.GetType();

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<ScalableMagnitudeAttribute>() is { } scalableAttr)
                    {
                        data.ScalableValue = scalableAttr.ToScalableFloat();
                    }
                    else if (field.GetCustomAttribute<AttributeMagnitudeAttribute>() is { } attrAttr)
                    {
                        data.AttributeValue = attrAttr.ToMagnitude();
                    }
                }
            }

            data.Stacking = GetStackingConfig(instance);
            return data;
        }
    }
}
