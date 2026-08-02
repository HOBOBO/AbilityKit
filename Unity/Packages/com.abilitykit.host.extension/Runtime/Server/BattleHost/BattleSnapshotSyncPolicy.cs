namespace AbilityKit.Ability.Host.Extensions.Server.BattleHost
{
    public sealed class BattleSnapshotSyncPolicy
    {
        public BattleSnapshotSyncPolicy(int fullSnapshotInterval = 30)
            : this(snapshotInterval: 1, fullSnapshotInterval: fullSnapshotInterval)
        {
        }

        public BattleSnapshotSyncPolicy(int snapshotInterval, int fullSnapshotInterval)
        {
            SnapshotInterval = snapshotInterval > 0 ? snapshotInterval : 1;
            FullSnapshotInterval = fullSnapshotInterval > 0 ? fullSnapshotInterval : 30;
        }

        public int SnapshotInterval { get; }

        public int FullSnapshotInterval { get; }

        public bool ShouldPublish(int observerCount, bool worldTicked)
        {
            return observerCount > 0 && worldTicked;
        }

        public bool ShouldPublish(int frame, int observerCount, bool worldTicked)
        {
            return ShouldPublish(observerCount, worldTicked)
                && (frame <= 0 || frame % SnapshotInterval == 0);
        }

        public bool ShouldCreateFullSnapshot(int frame)
        {
            return frame <= 0 || frame % FullSnapshotInterval == 0;
        }
    }
}
