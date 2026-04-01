using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 可选值包装器
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    [Serializable]
    public struct Optional<T> where T : struct
    {
        public T Value;
        public bool HasValue;

        public Optional(T value)
        {
            Value = value;
            HasValue = true;
        }

        public static Optional<T> None => default;
        public static Optional<T> Some(T value) => new Optional<T>(value);
        public T GetValueOrDefault(T defaultValue = default) => HasValue ? Value : defaultValue;
    }

    /// <summary>
    /// 可序列化的行为配置
    /// </summary>
    [Serializable]
    public class ExecutableConfig
    {
        public int TypeId;
        public string TypeName;
        public List<ExecutableConfig> Children;
        public ConditionConfig Condition;
        public Optional<ActionCallConfig> ActionCall;
        public ScheduleConfig Schedule;
        public Optional<DelayConfig> Delay;
        public SwitchConfig Switch;
        public Dictionary<string, float> FloatParams;
        public Dictionary<string, string> StringParams;
    }

    /// <summary>
    /// 可序列化的条件配置
    /// </summary>
    [Serializable]
    public class ConditionConfig
    {
        public int TypeId;
        public string TypeName;
        public int FieldId;
        public string CompareOp;
        public NumericValueRefDto CompareValue;
        public bool Negate;
        public NumericValueRefDto Left;
        public NumericValueRefDto Right;
        public List<ConditionConfig> Children;
        public string Combinator;
    }

    /// <summary>
    /// 可序列化的 Action 调用配置
    /// </summary>
    [Serializable]
    public struct ActionCallConfig
    {
        public int ActionId;
        public int Arity;
        public NumericValueRefDto Arg0;
        public NumericValueRefDto Arg1;
    }

    /// <summary>
    /// 可序列化的调度行为配置
    /// </summary>
    [Serializable]
    public struct ScheduleConfig
    {
        public string ScheduleMode;
        public float DurationMs;
        public float PeriodMs;
        public int MaxExecutions;
        public string Target;
        public List<ModifierDataConfig> Modifiers;
        public string ProjectilePrefab;
        public float ProjectileSpeed;
        public float ProjectileMaxDistance;
        public bool CanBeInterrupted;
    }

    /// <summary>
    /// 可序列化的延迟配置
    /// </summary>
    [Serializable]
    public struct DelayConfig
    {
        public float DelayMs;
    }

    /// <summary>
    /// 可序列化的 Switch 配置
    /// </summary>
    [Serializable]
    public class SwitchConfig
    {
        public string ValueSelector;
        public List<SwitchCaseConfig> Cases;
        public ExecutableConfig DefaultCase;
    }

    /// <summary>
    /// 可序列化的 Switch Case 配置
    /// </summary>
    [Serializable]
    public struct SwitchCaseConfig
    {
        public int Value;
        public ExecutableConfig Body;
    }

    /// <summary>
    /// 可序列化的修改器数据配置
    /// </summary>
    [Serializable]
    public struct ModifierDataConfig
    {
        public string Key;
        public float Value;
        public string ModifierType;
    }

    /// <summary>
    /// 可序列化的数值引用 DTO
    /// </summary>
    [Serializable]
    public struct NumericValueRefDto
    {
        public string Kind;
        public double ConstValue;
        public int BoardId;
        public int KeyId;
        public int FieldId;
        public string DomainId;
        public string Key;
        public string ExprText;
        public bool HasValue => !string.IsNullOrEmpty(Kind);
    }

    /// <summary>
    /// 可序列化的触发器计划 DTO
    /// </summary>
    [Serializable]
    public class TriggerPlanDto
    {
        public int TriggerId;
        public string EventName;
        public int EventId;
        public bool AllowExternal;
        public int Phase;
        public int Priority;
        public int InterruptPriority;
        public ConditionConfig Predicate;
        public List<ExecutableConfig> Executables;
        public string CueKind;
        public string CueVfxId;
        public string CueSfxId;
    }

    /// <summary>
    /// 可序列化的触发器数据库 DTO
    /// </summary>
    [Serializable]
    public class TriggerDatabaseDto
    {
        public List<TriggerPlanDto> Triggers;
        public Dictionary<int, string> Strings;
    }
}
