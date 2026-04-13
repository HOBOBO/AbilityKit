using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using AbilityKit.Core.Math;
using AbilityKit.Core.Common.Log;
using AbilityKit.World.ECS;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Snapshot
{
    public static class BattleActorSpawnApplier
    {
        public static void Apply(BattleContext ctx, MobaActorSpawnSnapshotCodec.Entry[] entries)
        {
            if (ctx == null) return;
            if (ctx.EntityWorld == null || ctx.EntityLookup == null || ctx.EntityFactory == null)
            {
                Log.Error("[BattleActorSpawnApplier] Apply ignored: BattleContext entity wiring not ready.");
                return;
            }
            if (entries == null || entries.Length == 0) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            var factory = ctx.EntityFactory;

            var dirty = ctx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new System.Collections.Generic.List<EC.IEntityId>(entries.Length);
                ctx.DirtyEntities = dirty;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var en = entries[i];
                if (en.NetId <= 0) continue;

                var netId = new BattleNetId(en.NetId);
                if (!lookup.TryResolve(world, netId, out var e))
                {
                    if (en.Kind == (int)SpawnEntityKind.Projectile)
                    {
                        e = factory.CreateProjectile(netId, ownerNetId: new BattleNetId(en.OwnerNetId), entityCode: en.Code);
                    }
                    else
                    {
                        e = factory.CreateCharacter(netId, entityCode: en.Code);
                    }
                }
                else
                {
                    if (e.TryGetRef(out BattleEntityMetaComponent meta) && meta != null)
                    {
                        meta.Kind = en.Kind == (int)SpawnEntityKind.Projectile ? BattleEntityKind.Projectile : BattleEntityKind.Character;
                        meta.EntityCode = en.Code;
                    }

                    if (en.Kind == (int)SpawnEntityKind.Projectile)
                    {
                        if (e.TryGetRef(out BattleProjectileComponent proj) && proj != null)
                        {
                            proj.OwnerNetId = new BattleNetId(en.OwnerNetId);
                        }
                    }
                }

                if (!e.TryGetRef(out BattleTransformComponent t) || t == null)
                {
                    t = new BattleTransformComponent();
                    e.WithRef(t);
                }

                t.Position = new Vector3(en.X, en.Y, en.Z);
                if (t.Forward == default) t.Forward = Vector3.forward;

                dirty.Add(e.Id);
            }
        }
    }
}
