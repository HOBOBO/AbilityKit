using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.Snapshot
{
    public static class BattleActorDespawnApplier
    {
        public static void Apply(BattleContext ctx, MobaActorDespawnSnapshotCodec.Entry[] entries)
        {
            if (ctx == null) return;
            if (ctx.EntityWorld == null || ctx.EntityLookup == null) return;
            if (entries == null || entries.Length == 0) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.ActorId <= 0) continue;

                var netId = new BattleNetId(e.ActorId);
                if (lookup.TryResolve(world, netId, out var entity))
                {
                    if (entity.IsValid) entity.Destroy();
                }

                lookup.Unbind(netId);
            }
        }
    }
}
