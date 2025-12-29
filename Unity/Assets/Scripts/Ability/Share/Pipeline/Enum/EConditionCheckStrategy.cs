namespace AbilityKit.Ability
{
    /// <summary>
    /// 条件检测策略枚举
    /// </summary>
    public enum EConditionCheckStrategy
    {
        /// <summary>
        /// 仅在进入时检查一次
        /// </summary>
        OnEnter,
        /// <summary>
        /// 持续检查
        /// </summary>
        Continuous,
        /// <summary>
        /// 在特定事件触发时检查
        /// </summary>
        OnEvent
    }
}