using System;
using System.Collections.Generic;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 策略上下文 — 携带策略执行所需的数据
    // ============================================================================

    /// <summary>
    /// 操作类型（用于区分不同的策略行为）
    /// 与具体的策略ID分离，操作类型由框架定义，策略可自行解释
    /// </summary>
    public enum StrategyOperationKind : byte
    {
        /// <summary>加法</summary>
        Add = 0,

        /// <summary>乘法</summary>
        Mult = 1,

        /// <summary>覆盖</summary>
        Override = 2,

        /// <summary>百分比加成</summary>
        PercentAdd = 3,

        /// <summary>添加（用于列表、标签等）</summary>
        ListAdd = 10,

        /// <summary>移除（用于列表、标签等）</summary>
        ListRemove = 11,

        /// <summary>替换（用于列表等）</summary>
        ListReplace = 12,

        /// <summary>保存并设置（用于状态）</summary>
        SaveAndSet = 20,

        /// <summary>还原（用于状态）</summary>
        Restore = 21,

        /// <summary>自定义操作开始标识</summary>
        Custom = 100,
    }

    /// <summary>
    /// 策略上下文。
    /// 包含策略执行所需的所有数据。
    /// 纯值类型，无堆分配。
    /// </summary>
    public readonly struct StrategyContext
    {
        #region 核心字段

        /// <summary>
        /// 策略ID（业务层定义）
        /// </summary>
        public StrategyId StrategyId { get; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public StrategyOperationKind OperationKind { get; }

        /// <summary>
        /// 目标Key（属性名、状态Key、列表项ID等）
        /// </summary>
        public string TargetKey { get; }

        /// <summary>
        /// 拥有者Key（用于按来源还原）
        /// </summary>
        public long OwnerKey { get; }

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 来源ID（业务层定义）
        /// </summary>
        public int SourceId { get; }

        /// <summary>
        /// 等级（用于缩放）
        /// </summary>
        public float Level { get; }

        #endregion

        #region 值存储

        /// <summary>
        /// 原始值对象
        /// </summary>
        private readonly object _value;

        /// <summary>
        /// 原始值类型（用于反序列化）
        /// </summary>
        private readonly StrategyValueType _valueType;

        #endregion

        #region 额外数据

        /// <summary>
        /// 额外数据（策略特定配置）
        /// </summary>
        private readonly Dictionary<string, object> _extraData;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建策略上下文
        /// </summary>
        public StrategyContext(
            StrategyId strategyId,
            StrategyOperationKind operationKind,
            string targetKey,
            long ownerKey,
            int priority = 0,
            int sourceId = 0,
            float level = 1f)
        {
            StrategyId = strategyId;
            OperationKind = operationKind;
            TargetKey = targetKey ?? string.Empty;
            OwnerKey = ownerKey;
            Priority = priority;
            SourceId = sourceId;
            Level = level;
            _value = null;
            _valueType = StrategyValueType.None;
            _extraData = null;
        }

        /// <summary>
        /// 创建带值的策略上下文
        /// </summary>
        public StrategyContext(
            StrategyId strategyId,
            StrategyOperationKind operationKind,
            string targetKey,
            object value,
            long ownerKey,
            int priority = 0,
            int sourceId = 0,
            float level = 1f)
        {
            StrategyId = strategyId;
            OperationKind = operationKind;
            TargetKey = targetKey ?? string.Empty;
            OwnerKey = ownerKey;
            Priority = priority;
            SourceId = sourceId;
            Level = level;
            _value = value;
            _valueType = InferValueType(value);
            _extraData = null;
        }

        private static StrategyValueType InferValueType(object value)
        {
            if (value == null) return StrategyValueType.None;

            return value switch
            {
                float => StrategyValueType.Float,
                int => StrategyValueType.Int,
                bool => StrategyValueType.Bool,
                string => StrategyValueType.String,
                _ => StrategyValueType.Object
            };
        }

        #endregion

        #region 值访问

        /// <summary>
        /// 获取原始值对象
        /// </summary>
        public object Value => _value;

        /// <summary>
        /// 值类型
        /// </summary>
        public StrategyValueType ValueType => _valueType;

        /// <summary>
        /// 是否有值
        /// </summary>
        public bool HasValue => _value != null;

        /// <summary>
        /// 获取浮点值
        /// </summary>
        public float GetFloatValue()
        {
            if (_value is float f) return f;
            if (_value is int i) return i;
            if (_value is double d) return (float)d;
            if (_value is long l) return l;
            if (_value is string s && float.TryParse(s, out var result)) return result;
            return 0f;
        }

        /// <summary>
        /// 尝试获取浮点值
        /// </summary>
        public bool TryGetFloatValue(out float value)
        {
            if (_value is float f)
            {
                value = f;
                return true;
            }
            if (_value is int i)
            {
                value = i;
                return true;
            }
            if (_value is double d)
            {
                value = (float)d;
                return true;
            }
            if (_value is long l)
            {
                value = l;
                return true;
            }
            if (_value is string s && float.TryParse(s, out var result))
            {
                value = result;
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>
        /// 获取整数值
        /// </summary>
        public int GetIntValue()
        {
            if (_value is int i) return i;
            if (_value is float f) return (int)f;
            if (_value is long l) return (int)l;
            if (_value is string s && int.TryParse(s, out var result)) return result;
            return 0;
        }

        /// <summary>
        /// 尝试获取整数值
        /// </summary>
        public bool TryGetIntValue(out int value)
        {
            if (_value is int i)
            {
                value = i;
                return true;
            }
            if (_value is float f)
            {
                value = (int)f;
                return true;
            }
            if (_value is long l)
            {
                value = (int)l;
                return true;
            }
            if (_value is string s && int.TryParse(s, out var result))
            {
                value = result;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// 获取布尔值
        /// </summary>
        public bool GetBoolValue()
        {
            if (_value is bool b) return b;
            if (_value is float f) return f > 0.5f;
            if (_value is int i) return i != 0;
            if (_value is string s) return bool.TryParse(s, out var result) && result;
            return false;
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string GetStringValue()
        {
            return _value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 获取对象值（类型安全）
        /// </summary>
        public T GetValue<T>()
        {
            if (_value is T t) return t;
            return default;
        }

        /// <summary>
        /// 尝试获取对象值
        /// </summary>
        public bool TryGetValue<T>(out T value)
        {
            if (_value is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        #endregion

        #region 额外数据访问

        /// <summary>
        /// 获取额外数据
        /// </summary>
        public T GetExtraData<T>(string key)
        {
            if (_extraData == null) return default;
            if (_extraData.TryGetValue(key, out var value) && value is T t) return t;
            return default;
        }

        /// <summary>
        /// 尝试获取额外数据
        /// </summary>
        public bool TryGetExtraData<T>(string key, out T value)
        {
            if (_extraData != null && _extraData.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }
            value = default;
            return false;
        }

        #endregion

        #region 工厂方法

        /// <summary>
        /// 创建数值修改上下文
        /// </summary>
        public static StrategyContext Numeric(
            string strategyId,
            StrategyOperationKind op,
            string targetKey,
            float value,
            long ownerKey,
            int priority = 10,
            int sourceId = 0,
            float level = 1f)
        {
            return new StrategyContext(
                new StrategyId(strategyId),
                op,
                targetKey,
                value,
                ownerKey,
                priority,
                sourceId,
                level
            );
        }

        /// <summary>
        /// 创建状态修改上下文
        /// </summary>
        public static StrategyContext State(
            string strategyId,
            StrategyOperationKind op,
            string stateKey,
            object value,
            long ownerKey,
            int priority = 0,
            int sourceId = 0)
        {
            return new StrategyContext(
                new StrategyId(strategyId),
                op,
                stateKey,
                value,
                ownerKey,
                priority,
                sourceId,
                1f
            );
        }

        /// <summary>
        /// 创建标签操作上下文
        /// </summary>
        public static StrategyContext Tag(
            string strategyId,
            StrategyOperationKind op,
            string tag,
            long ownerKey,
            int sourceId = 0)
        {
            return new StrategyContext(
                new StrategyId(strategyId),
                op,
                tag,
                null,
                ownerKey,
                0,
                sourceId,
                1f
            );
        }

        #endregion

        #region Object

        public override string ToString()
        {
            return $"StrategyContext({StrategyId}, Op={OperationKind}, Target={TargetKey}, Value={_value}, Owner={OwnerKey})";
        }

        #endregion
    }

    /// <summary>
    /// 策略值类型
    /// </summary>
    public enum StrategyValueType : byte
    {
        None = 0,
        Float = 1,
        Int = 2,
        Bool = 3,
        String = 4,
        Object = 5,
    }

    // ============================================================================
    // 策略数据 — 可序列化的策略配置
    // ============================================================================

    /// <summary>
    /// 策略数据。
    /// 用于配置驱动的策略执行。
    /// 可序列化，适合存储在配置文件中。
    /// </summary>
    [Serializable]
    public struct StrategyData
    {
        /// <summary>
        /// 策略ID（业务层定义）
        /// </summary>
        public string StrategyId;

        /// <summary>
        /// 操作类型
        /// </summary>
        public StrategyOperationKind OperationKind;

        /// <summary>
        /// 目标Key
        /// </summary>
        public string TargetKey;

        /// <summary>
        /// 值（JSON序列化时会自动处理）
        /// </summary>
        public object Value;

        /// <summary>
        /// 拥有者Key
        /// </summary>
        public long OwnerKey;

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority;

        /// <summary>
        /// 来源ID
        /// </summary>
        public int SourceId;

        /// <summary>
        /// 等级
        /// </summary>
        public float Level;

        /// <summary>
        /// 额外数据
        /// </summary>
        public Dictionary<string, object> ExtraData;

        #region 工厂方法

        /// <summary>
        /// 获取浮点值
        /// </summary>
        public float GetFloatValue()
        {
            if (Value is float f) return f;
            if (Value is int i) return i;
            if (Value is double d) return (float)d;
            if (Value is long l) return l;
            if (Value is string s && float.TryParse(s, out var result)) return result;
            return 0f;
        }

        /// <summary>
        /// 尝试获取浮点值
        /// </summary>
        public bool TryGetFloatValue(out float value)
        {
            if (Value is float f)
            {
                value = f;
                return true;
            }
            if (Value is int i)
            {
                value = i;
                return true;
            }
            if (Value is double d)
            {
                value = (float)d;
                return true;
            }
            if (Value is long l)
            {
                value = l;
                return true;
            }
            if (Value is string s && float.TryParse(s, out var result))
            {
                value = result;
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>
        /// 创建数值策略数据
        /// </summary>
        public static StrategyData Numeric(
            string strategyId,
            StrategyOperationKind op,
            string targetKey,
            float value,
            long ownerKey,
            int priority = 10,
            int sourceId = 0,
            float level = 1f)
        {
            return new StrategyData
            {
                StrategyId = strategyId,
                OperationKind = op,
                TargetKey = targetKey,
                Value = value,
                OwnerKey = ownerKey,
                Priority = priority,
                SourceId = sourceId,
                Level = level
            };
        }

        /// <summary>
        /// 创建状态策略数据
        /// </summary>
        public static StrategyData State(
            string strategyId,
            StrategyOperationKind op,
            string stateKey,
            object value,
            long ownerKey,
            int priority = 0,
            int sourceId = 0)
        {
            return new StrategyData
            {
                StrategyId = strategyId,
                OperationKind = op,
                TargetKey = stateKey,
                Value = value,
                OwnerKey = ownerKey,
                Priority = priority,
                SourceId = sourceId,
                Level = 1f
            };
        }

        /// <summary>
        /// 创建标签策略数据
        /// </summary>
        public static StrategyData Tag(
            string strategyId,
            StrategyOperationKind op,
            string tag,
            long ownerKey,
            int sourceId = 0)
        {
            return new StrategyData
            {
                StrategyId = strategyId,
                OperationKind = op,
                TargetKey = tag,
                Value = null,
                OwnerKey = ownerKey,
                Priority = 0,
                SourceId = sourceId,
                Level = 1f
            };
        }

        #endregion

        #region 转换

        /// <summary>
        /// 转换为策略上下文
        /// </summary>
        public StrategyContext ToContext()
        {
            return new StrategyContext(
                new StrategyId(StrategyId),
                OperationKind,
                TargetKey,
                Value,
                OwnerKey,
                Priority,
                SourceId,
                Level
            );
        }

        #endregion
    }

    // ============================================================================
    // 策略实例 — 运行时策略执行
    // ============================================================================

    /// <summary>
    /// 策略实例。
    /// 包含策略数据和运行时信息。
    /// </summary>
    public sealed class StrategyInstance
    {
        /// <summary>
        /// 实例唯一ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 策略数据
        /// </summary>
        public StrategyData Data { get; set; }

        /// <summary>
        /// 策略实例（运行时解析）
        /// </summary>
        public IStrategy Strategy { get; set; }

        /// <summary>
        /// 拥有者Key
        /// </summary>
        public long OwnerKey => Data.OwnerKey;

        /// <summary>
        /// 目标Key
        /// </summary>
        public string TargetKey => Data.TargetKey;

        /// <summary>
        /// 策略ID
        /// </summary>
        public StrategyId StrategyId => new StrategyId(Data.StrategyId);

        /// <summary>
        /// 优先级
        /// </summary>
        public int Priority => Data.Priority;

        /// <summary>
        /// 转换为上下文
        /// </summary>
        public StrategyContext ToContext() => Data.ToContext();
    }
}
