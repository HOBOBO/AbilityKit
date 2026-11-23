namespace AbilityKit.Triggering.Runtime.Config
{
    /// <summary>
    /// Cue 类型枚举
    /// </summary>
    public enum ECueKind : byte
    {
        None = 0,
        Descriptor = 1,
    }

    /// <summary>
    /// Cue 归属级别。
    /// Trigger 级别绑定一次触发器调度生命周期；Behavior 级别绑定具体行为/Action 生命周期。
    /// </summary>
    public enum ECueLevel : byte
    {
        None = 0,
        Trigger = 1,
        Behavior = 2,
    }

    /// <summary>
    /// Cue 生命周期阶段。
    /// 仅表达触发器框架自身会派发的通用生命周期；业务特定生命周期应由业务层通过 CueData/CuePayload 扩展。
    /// </summary>
    public enum ECueLifecycleStage : byte
    {
        None = 0,
        ConditionPassed = 1,
        ConditionFailed = 2,
        BeforeAction = 3,
        Executed = 4,
        Interrupted = 5,
        Skipped = 6,
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
        /// <summary>持续调度（按间隔驱动，生命周期由外部中断或执行次数控制）</summary>
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
        Function = 5,
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

    /// <summary>
    /// Action 调度模式
    /// 定义单个 Action 被 Trigger 激活后的执行方式
    /// </summary>
    public enum EActionScheduleMode : byte
    {
        /// <summary>立即执行一次（默认）</summary>
        Immediate = 0,
        /// <summary>延迟执行（启动后等待指定时间）</summary>
        Delayed = 1,
        /// <summary>周期执行（按间隔重复，可设置最大次数）</summary>
        Periodic = 2,
        /// <summary>持续调度执行（按间隔执行，生命周期由外部中断或执行次数控制）</summary>
        Continuous = 3,
        /// <summary>时间线执行（按时间线序列执行多个子Action）</summary>
        Timeline = 4,
    }

    /// <summary>
    /// Action 执行策略
    /// 定义单次执行时的环境和约束
    /// </summary>
    public enum EActionExecutionPolicy : byte
    {
        /// <summary>立即执行（默认）</summary>
        Immediate = 0,
        /// <summary>
        /// 兼容保留的策略，不建议新计划使用。调度路径仅防止同一执行器实例同步重入；
        /// 不提供跨动作队列、先进先出或优先级排序。立即动作不使用此包装器。
        /// </summary>
        Queued = 1,
        /// <summary>并行执行（不等待队列）</summary>
        Parallel = 2,
        /// <summary>延迟到下一帧执行</summary>
        Deferred = 3,
        /// <summary>支持回滚（执行失败时自动回滚）</summary>
        WithRollback = 4,
        /// <summary>失败重试（可配置重试次数）</summary>
        WithRetry = 5,
        /// <summary>条件执行（满足条件才执行）</summary>
        Conditional = 6,
    }
}
