namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 条件评估上下文接口
    /// 运行时使用此接口提供状态机状态给条件进行评估
    /// </summary>
    public interface IHfsmEvaluationContext
    {
        /// <summary>
        /// 获取布尔类型参数的值
        /// </summary>
        bool GetBool(string parameterName);

        /// <summary>
        /// 获取浮点数参数的值
        /// </summary>
        float GetFloat(string parameterName);

        /// <summary>
        /// 获取整数参数的值
        /// </summary>
        int GetInt(string parameterName);

        /// <summary>
        /// 获取触发器参数的状态
        /// </summary>
        bool GetTrigger(string parameterName);

        /// <summary>
        /// 获取指定节点的所有行为是否完成
        /// </summary>
        bool HasAllActionsCompleted(string nodeId);

        /// <summary>
        /// 获取指定节点已经过的时间（秒）
        /// </summary>
        float GetNodeElapsedTime(string nodeId);

        /// <summary>
        /// 检查指定状态机中是否有指定状态处于激活状态
        /// </summary>
        bool IsStateActive(string stateMachineId, string stateId);
    }
}
