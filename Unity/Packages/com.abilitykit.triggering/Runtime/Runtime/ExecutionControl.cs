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

        public void Reset()
        {
            InterruptMode = EInterruptMode.None;
            InterruptPriority = 0;
            InterruptConditionPassed = false;
            InterruptTriggerId = 0;
            InterruptSourceName = null;
            StopPropagation = false;
            Cancel = false;
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
        /// 打断所有优先级低于指定值的触发器
        /// </summary>
        /// <param name="triggerPriority">当前触发器的优先级（作为阈值）</param>
        /// <param name="conditionPassed">是否条件成功触发</param>
        /// <param name="triggerId">触发器标识（用于溯源）</param>
        /// <param name="sourceName">来源名称（用于调试）</param>
        public void StopBelowPriority(int triggerPriority, bool conditionPassed, int triggerId = 0, string sourceName = null)
        {
            StopPropagation = true;
            InterruptMode = EInterruptMode.BelowPriority;
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
            if (!StopPropagation) return false;

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
