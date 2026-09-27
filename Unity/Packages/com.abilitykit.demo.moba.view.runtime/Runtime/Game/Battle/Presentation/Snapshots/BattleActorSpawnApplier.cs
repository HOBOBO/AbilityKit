using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Snapshot
{
    /// <summary>
    /// Applies actor-spawn contracts received by the command-handler route.
    /// </summary>
    public static class BattleActorSpawnApplier
    {
        public static void Apply(BattleContext ctx, ActorSpawnData[] entries)
        {
            BattleSnapshotEntityApplier.ApplySpawn(
                ctx,
                entries,
                updateExisting: true,
                logContext: nameof(BattleActorSpawnApplier));
        }
    }
}
