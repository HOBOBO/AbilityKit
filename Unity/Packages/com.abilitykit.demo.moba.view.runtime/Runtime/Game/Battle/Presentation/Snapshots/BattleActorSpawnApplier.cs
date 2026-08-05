using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using UnityEngine;
using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Logging;
using AbilityKit.World.ECS;
using AbilityKit.Protocol.Moba.StateSync;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Snapshot
{
    /// <summary>
    /// ActorSpawn 快照应用器（特性驱动路径）。
    ///
    /// **与 <c>BattleSnapshotEntityApplier.ApplySpawn</c> 的关系（2026-07-20 核校）**：
    /// 本类被 <c>BattleSnapshotDeclarations.HandleActorSpawn</c>（经
    /// <c>[SnapshotCmdHandler]</c> 特性注册）调用；而
    /// <c>BattleSnapshotEntityApplier.ApplySpawn</c> 被
    /// <c>BattleSyncFeature.OnActorSpawnSnapshot</c> + <c>ConfirmedViewSnapshotRuntime</c>
    /// 直接调用。两者**并行存在**，逻辑高度重合（都做 CreateCharacter/CreateProjectile
    /// + 写 BattleTransformComponent），但在不同 SyncMode / 路径下触发。
    ///
    /// 未来清理建议（不阻塞演示级联机）：确认两条路径在不同 SyncMode 下的实际触发条件，
    /// 若确实等价则统一到 <c>BattleSnapshotEntityApplier.ApplySpawn</c>（更通用，支持
    /// spawn + transform 合并路径），删除本类并把 <c>BattleSnapshotDeclarations</c>
    /// 的特性 handler 改为委托。改造前必须跑两条 headless 路径的回归（Lockstep 与
    /// SnapshotAuthority）。
    /// </summary>
    public static class BattleActorSpawnApplier
    {
        public static void Apply(BattleContext ctx, MobaActorSpawnSnapshotEntry[] entries)
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
