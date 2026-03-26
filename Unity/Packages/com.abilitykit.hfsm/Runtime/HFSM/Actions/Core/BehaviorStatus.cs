namespace UnityHFSM.Actions
{
    /// <summary>
    /// 行为执行结果
    /// </summary>
    public enum BehaviorStatus
    {
        /// <summary>行为正在运行</summary>
        Running,

        /// <summary>行为成功完成</summary>
        Success,

        /// <summary>行为失败</summary>
        Failure
    }
}
