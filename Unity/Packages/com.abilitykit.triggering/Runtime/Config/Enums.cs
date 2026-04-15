namespace AbilityKit.Triggering.Runtime.Config
{
    /// <summary>
    /// Cue 类型枚举
    /// </summary>
    public enum ECueKind : byte
    {
        None = 0,
        Vfx = 1,
        Sfx = 2,
        VfxSfx = 3,
        Custom = 4,
    }

    /// <summary>
    /// 调度模式枚举
    /// 定义触发器的执行时机和方式
    /// </summary>
    public enum EScheduleMode : byte
    {
        /// <summary>瞬时执行（事件触发，无延迟）</summary>
        Transient = 0,
        /// <summary>延迟执行（定时一次性）</summary>
        Timed = 1,
        /// <summary>周期执行（按间隔重复）</summary>
        Periodic = 2,
        /// <summary>外部控制（如Buff系统手动触发）</summary>
        External = 3,
        /// <summary>条件触发（满足条件时执行，Phase驱动）</summary>
        Conditional = 4,
        /// <summary>持续行为（每帧驱动直到外部终止）</summary>
        Continuous = 5,
    }

    /// <summary>
    /// 条件类型枚举
    /// </summary>
    public enum EPredicateKind : byte
    {
        None = 0,
        Function = 1,
        Expression = 2,
        Blackboard = 3,
        /// <summary>距离检查条件</summary>
        DistanceCheck = 10,
        /// <summary>生命值检查条件</summary>
        HealthCheck = 11,
    }

    /// <summary>
    /// 布尔表达式节点类型枚举
    /// </summary>
    public enum EBoolExprNodeKind : byte
    {
        Const = 0,
        Not = 1,
        And = 2,
        Or = 3,
        CompareNumeric = 4,
    }

    /// <summary>
    /// 比较操作符枚举
    /// </summary>
    public enum ECompareOp : byte
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        GreaterThanOrEqual = 3,
        LessThan = 4,
        LessThanOrEqual = 5,
    }

    /// <summary>
    /// 条件组合类型
    /// </summary>
    public enum EConditionCombinator : byte
    {
        And,
        Or,
    }
}