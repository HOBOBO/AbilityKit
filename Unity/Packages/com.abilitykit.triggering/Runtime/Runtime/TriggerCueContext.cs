namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 触发器 Cue 回调的上下文
    /// 携带触发器调度时的全部信息，供 Cue 层渲染使用
    /// </summary>
    public readonly struct TriggerCueContext
    {
        /// <summary>事件 ID（StableStringId 或 IntId）</summary>
        public readonly int EventId;

        /// <summary>事件类型名称</summary>
        public readonly string EventName;

        /// <summary>事件参数（Payload）</summary>
        public readonly object Args;

        /// <summary>触发器 Phase</summary>
        public readonly int Phase;

        /// <summary>触发器 Priority</summary>
        public readonly int Priority;

        /// <summary>触发器注册顺序号（全局自增，用于同 Phase/Priority 时的稳定排序）</summary>
        public readonly long Order;

        /// <summary>触发器唯一标识（TriggerPlan.TriggerId）</summary>
        public readonly int TriggerId;

        /// <summary>触发器类型名称（用于调试溯源）</summary>
        public readonly string TriggerTypeName;

        /// <summary>打断原因（当触发器被跳过或打断时有效）</summary>
        public readonly ETriggerShortCircuitReason InterruptReason;

        /// <summary>打断来源名称（用于调试）</summary>
        public readonly string InterruptSourceName;

        /// <summary>打断触发器的 TriggerId（用于溯源）</summary>
        public readonly int InterruptTriggerId;

        /// <summary>打断时条件是否通过</summary>
        public readonly bool InterruptConditionPassed;

        /// <summary>打断控制句柄（可能为 null）</summary>
        public readonly ExecutionControl Control;

        public TriggerCueContext(
            int eventId,
            string eventName,
            object args,
            int phase,
            int priority,
            long order,
            int triggerId,
            string triggerTypeName,
            ETriggerShortCircuitReason interruptReason,
            string interruptSourceName,
            int interruptTriggerId,
            bool interruptConditionPassed,
            ExecutionControl control)
        {
            EventId = eventId;
            EventName = eventName;
            Args = args;
            Phase = phase;
            Priority = priority;
            Order = order;
            TriggerId = triggerId;
            TriggerTypeName = triggerTypeName;
            InterruptReason = interruptReason;
            InterruptSourceName = interruptSourceName;
            InterruptTriggerId = interruptTriggerId;
            InterruptConditionPassed = interruptConditionPassed;
            Control = control;
        }
    }
}
