namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// debug_log Action 的强类型参数
    /// </summary>
    public readonly struct DebugLogArgs
    {
        /// <summary>
        /// 消息ID（从 TriggerPlanJsonDatabase 的 string table 中获取）
        /// 为0时表示无消息ID，仅输出上下文信息
        /// </summary>
        public readonly int MsgId;

        /// <summary>
        /// 是否输出完整上下文信息（dump）
        /// </summary>
        public readonly bool Dump;

        public DebugLogArgs(int msgId, bool dump)
        {
            MsgId = msgId;
            Dump = dump;
        }

        public static DebugLogArgs Default => new DebugLogArgs(0, false);

        /// <summary>
        /// 无参数版本（仅输出上下文信息）
        /// </summary>
        public static DebugLogArgs Empty => default;
    }
}
