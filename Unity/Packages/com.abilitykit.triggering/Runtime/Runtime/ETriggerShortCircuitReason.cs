namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 触发器短路原因枚举（观察者使用）
    /// 与 ShortCircuitReason 保持一致
    /// </summary>
    public enum ETriggerShortCircuitReason
    {
        None = 0,
        StopPropagation = 1,
        Cancel = 2,
        ParentBlocked = 3,
        ConditionFailed = 4,
        ActionInterrupted = 5,
        LimitReached = 6,
        GuardFailed = 7,
        InterruptedByHigherPriority = 8,
        InterruptedByFailedCondition = 9,
    }
}
