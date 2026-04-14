namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 定义条件都不满足时的行为
    /// </summary>
    public enum ENoConditionBehavior
    {
        /// <summary>
        /// 等待直到有条件满足
        /// </summary>
        Wait,
        /// <summary>
        /// 完成当前阶段
        /// </summary>
        Complete,
        /// <summary>
        /// 中断并失败
        /// </summary>
        Fail,
        /// <summary>
        /// 跳过当前阶段
        /// </summary>
        Skip
    }
}
