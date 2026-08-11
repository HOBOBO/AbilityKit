namespace AbilityKit.Game.Flow
{
    public enum BattleViewEventSourceMode
    {
        SnapshotOnly = 0,
        TriggerOnly = 1,
        Hybrid = 2,
    }

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
}
