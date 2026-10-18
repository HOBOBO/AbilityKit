namespace AbilityKit.Game.Flow
{
    public enum BattleViewEventSourceMode
    {
        SnapshotOnly = 0,
        TriggerOnly = 1,
        Hybrid = 2,
    }

    public sealed class ViewEventSourceModePolicy
    {
        public BattleViewEventSourceMode Resolve(BattleViewEventSourceMode? mode)
        {
            return mode ?? BattleViewEventSourceMode.SnapshotOnly;
        }

        public bool ShouldUseTriggerAdapter(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid;
        }

        public bool ShouldUseSnapshotAdapter(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid;
        }
    }
}
