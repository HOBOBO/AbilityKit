namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 打断模式，控制打断信号对后续触发器的影响范围
    /// </summary>
    public enum EInterruptMode
    {
        /// <summary>
        /// 无打断效果（但仍记录到生命周期钩子）
        /// </summary>
        None = 0,

        /// <summary>
        /// 打断所有后续触发器
        /// </summary>
        All = 1,

        /// <summary>
        /// 仅打断优先级严格低于阈值的触发器
        /// </summary>
        BelowPriority = 2,

        /// <summary>
        /// 仅打断优先级不高于阈值的触发器（用于"低优先级服从高优先级"）
        /// </summary>
        BelowOrEqualPriority = 3,
    }

    /// <summary>
    /// Runner 级别的打断策略，决定条件评估失败时是否短路后续触发器
    /// </summary>
    public enum EInterruptPolicy
    {
        /// <summary>
        /// 不短路：Evaluate 返回 false 不会打断后续触发器，仅跳过当前触发器
        /// </summary>
        None = 0,

        /// <summary>
        /// 严格短路：Evaluate 返回 false 立即中断所有后续触发器
        /// </summary>
        Strict = 1,
    }

    /// <summary>
    /// 执行控制上下文
    /// 由触发器在 Evaluate/Execute 过程中写入，决定后续触发器的命运
    /// </summary>
    public sealed class ExecutionControl
    {
        /// <summary>
        /// 打断模式，决定打断信号对后续触发器的影响范围
        /// </summary>
        public EInterruptMode InterruptMode = EInterruptMode.None;

        /// <summary>
        /// 打断优先级阈值（由当前触发器写入）
        /// </summary>
        public int InterruptPriority;

        /// <summary>
        /// 当前打断是否由条件成功触发（true）还是条件失败触发（false）
        /// </summary>
        public bool InterruptConditionPassed;

        /// <summary>
        /// 打断触发器的 TriggerId（用于调试溯源）
        /// </summary>
        public int InterruptTriggerId;

        /// <summary>
        /// 触发打断的行为名称（用于调试）
        /// </summary>
        public string InterruptSourceName;

        /// <summary>
        /// 显式打断：要求立即停止传播所有后续触发器
        /// </summary>
        public bool StopPropagation;

        /// <summary>
        /// 取消标志（向后兼容，效果同 StopPropagation）
        /// </summary>
        public bool Cancel;

        /// <summary>
        /// 当前计划中的动作是否因业务前置条件不满足而拒绝执行。
        /// </summary>
        public bool IsActionRejected { get; private set; }

        /// <summary>
        /// 首个动作拒绝原因，用于向计划调用方传播稳定的失败信息。
        /// </summary>
        public string ActionRejectReason { get; private set; }

        /// <summary>
        /// 是否已经请求硬停止。硬停止会终止事件通道和 runner 的后续分发。
        /// </summary>
        public bool IsHardStopped => StopPropagation || Cancel || InterruptMode == EInterruptMode.All;

        /// <summary>
        /// 是否应停止当前计划的后续动作。动作拒绝仅短路当前计划，不影响事件通道。
        /// </summary>
        public bool IsPlanStopped => IsHardStopped || IsActionRejected;

        /// <summary>
        /// 是否存在按优先级过滤的软中断。软中断只跳过命中的后续触发器，不终止整个分发。
        /// </summary>
        public bool HasPriorityInterrupt => InterruptMode == EInterruptMode.BelowPriority || InterruptMode == EInterruptMode.BelowOrEqualPriority;

        public void Reset()
        {
            InterruptMode = EInterruptMode.None;
            InterruptPriority = 0;
            InterruptConditionPassed = false;
            InterruptTriggerId = 0;
            InterruptSourceName = null;
            StopPropagation = false;
            Cancel = false;
            IsActionRejected = false;
            ActionRejectReason = null;
        }

        /// <summary>
        /// 拒绝当前动作并停止当前计划中的后续动作。重复拒绝时保留首个原因。
        /// </summary>
        public void RejectAction(string reason)
        {
            if (IsActionRejected) return;

            IsActionRejected = true;
            ActionRejectReason = reason;
        }

        /// <summary>
        /// 显式打断所有后续触发器（向后兼容）
        /// </summary>
        public void StopAll()
        {
            StopPropagation = true;
            InterruptMode = EInterruptMode.All;
        }

        /// <summary>
        /// 跳过所有优先级低于指定值的后续触发器，不终止整个事件分发。
        /// </summary>
        /// <param name="triggerPriority">当前触发器的优先级（作为阈值）</param>
        /// <param name="conditionPassed">是否条件成功触发</param>
        /// <param name="triggerId">触发器标识（用于溯源）</param>
        /// <param name="sourceName">来源名称（用于调试）</param>
        public void StopBelowPriority(int triggerPriority, bool conditionPassed, int triggerId = 0, string sourceName = null)
        {
            InterruptMode = EInterruptMode.BelowPriority;
            InterruptPriority = triggerPriority;
            InterruptConditionPassed = conditionPassed;
            InterruptTriggerId = triggerId;
            InterruptSourceName = sourceName;
        }

        /// <summary>
        /// 跳过所有优先级不高于指定值的后续触发器，不终止整个事件分发。
        /// </summary>
        public void StopBelowOrEqualPriority(int triggerPriority, bool conditionPassed, int triggerId = 0, string sourceName = null)
        {
            InterruptMode = EInterruptMode.BelowOrEqualPriority;
            InterruptPriority = triggerPriority;
            InterruptConditionPassed = conditionPassed;
            InterruptTriggerId = triggerId;
            InterruptSourceName = sourceName;
        }

        /// <summary>
        /// 检查给定优先级的触发器是否应被当前打断信号拦截
        /// </summary>
        public bool ShouldBlock(int targetPhase, int targetPriority)
        {
            if (IsHardStopped) return true;
            if (!HasPriorityInterrupt) return false;

            switch (InterruptMode)
            {
                case EInterruptMode.None:
                    return false;
                case EInterruptMode.All:
                    return true;
                case EInterruptMode.BelowPriority:
                    return targetPriority < InterruptPriority;
                case EInterruptMode.BelowOrEqualPriority:
                    return targetPriority <= InterruptPriority;
                default:
                    return true;
            }
        }
    }
}
