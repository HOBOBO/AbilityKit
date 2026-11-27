namespace AbilityKit.Game.Flow
{
    public enum BattleSyncMode
    {
        Lockstep = 0,
        SnapshotAuthority = 1,
        HybridPredictReconcile = 2,
    }

    /// <summary>Where the authoritative simulation runs (local vs gateway remote).</summary>
    public enum BattleHostMode
    {
        Local = 0,
        GatewayRemote = 1,
    }

    /// <summary>Whether the session runs normally, records, or replays.</summary>
    public enum BattleRunMode
    {
        Normal = 0,
        Record = 1,
        Replay = 2,
    }

    /// <summary>
    /// 对局录制的轨道范围，不改变同步模式或世界的恢复策略。
    /// </summary>
    public enum BattleRecordCoverage
    {
        /// <summary>
        /// 只记录输入和可用的状态哈希，使用优化二进制。
        /// </summary>
        InputsAndHashes = 0,

        /// <summary>
        /// 追加调用链已收到的快照，不要求同步端额外生成快照。
        /// </summary>
        WithSnapshots = 1,
    }
}
