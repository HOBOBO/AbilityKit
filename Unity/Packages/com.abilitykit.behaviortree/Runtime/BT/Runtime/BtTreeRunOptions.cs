namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 树实例运行选项。种子决定全部节点随机子流的派生基准；
    /// DebugName 非空时实例自动注册进 <see cref="BtDebugRegistry"/>（编辑器拉取观察）。
    /// </summary>
    public sealed class BtTreeRunOptions
    {
        public ulong Seed { get; set; } = 0x12345678UL;
        /// <summary>主栈清空后自动重启。默认 false；响应式接入（如 MOBA）读根状态后显式 Restart。</summary>
        public bool RestartWhenComplete { get; set; }
        public string? DebugName { get; set; }
        public string? DebugOwnerLabel { get; set; }
    }
}
