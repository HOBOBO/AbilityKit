using AbilityKit.Demo.Moba.Share;

namespace AbilityKit.Game.Flow.Battle.Snapshot
{
    public static class BattleActorDespawnApplier
    {
        public static void Apply(BattleContext ctx, ActorDespawnData[] entries)
        {
            BattleSnapshotEntityApplier.ApplyDespawn(ctx, entries);
        }
    }
}
