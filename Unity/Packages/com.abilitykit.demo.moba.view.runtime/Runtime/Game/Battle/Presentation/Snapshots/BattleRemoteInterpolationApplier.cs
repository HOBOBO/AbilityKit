using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 将 <see cref="GatewayStateSyncSnapshot"/> 中插值后的 actor 状态应用到 view EntityWorld。
    ///
    /// 与 snapshot 通道的 BattleSnapshotEntityApplier 的关系：
    /// - BattleSnapshotEntityApplier 处理 MobaActorTransformSnapshotEntry（旧 codec 路径）。
    /// - 本类处理 GatewayStateSyncActorSnapshot（插值后的 state-sync push）。
    /// - 两者共用同一个 view EntityWorld 和 BattleEntityLookup。
    ///
    /// 启用客户端预测时，本地玩家的 transform 由 PredictionViewBridge 负责；
    /// 纯权威快照模式下，本地玩家与远端玩家一样应用插值结果。
    /// </summary>
    internal static class BattleRemoteInterpolationApplier
    {
        internal static int ResolveExcludedLocalActorId(
            bool enableClientPrediction,
            int localActorId)
        {
            return enableClientPrediction ? localActorId : 0;
        }

        /// <summary>
        /// 将插值后的 snapshot 应用到 view EntityWorld 中的远端实体。
        /// State-sync 快照是完整的 actor 表现来源，不依赖旧 codec 的 spawn 快照先到达。
        /// </summary>
        /// <param name="entityContext">View entity capabilities used to resolve or create actors.</param>
        /// <param name="snapshot">插值后的 actor 列表</param>
        /// <param name="localActorId">本地玩家 actorId（跳过，由 PredictionViewBridge 负责）</param>
        public static void Apply(
            IBattleEntityContext entityContext,
            in GatewayStateSyncSnapshot snapshot,
            int localActorId)
        {
            if (entityContext == null) return;

            var world = entityContext.EntityWorld;
            var lookup = entityContext.EntityLookup;
            var factory = entityContext.EntityFactory;
            if (world == null || lookup == null || factory == null) return;

            var actors = snapshot.Actors ?? System.Array.Empty<GatewayStateSyncActorSnapshot>();
            var dirty = entityContext.DirtyEntities;
            if (dirty == null)
            {
                dirty = new List<IEntityId>(actors.Length);
                entityContext.DirtyEntities = dirty;
            }

            var authoritativeActorIds = snapshot.IsFullSnapshot
                ? new HashSet<int>()
                : null;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor.ActorId <= 0) continue;

                authoritativeActorIds?.Add(actor.ActorId);
                if (actor.ActorId == localActorId) continue;

                var netId = new BattleNetId(actor.ActorId);
                if (!lookup.TryResolve(world, netId, out var entity))
                {
                    entity = actor.Kind == (int)SpawnEntityKind.Projectile
                        ? factory.CreateProjectile(
                            netId,
                            new BattleNetId(actor.OwnerNetId),
                            actor.Code)
                        : factory.CreateCharacter(netId, actor.Code);
                }

                if (!entity.TryGetRef(out BattleTransformComponent transform) || transform == null)
                {
                    transform = new BattleTransformComponent();
                    entity.WithRef(transform);
                }

                transform.Position = new Vector3(actor.X, actor.Y, actor.Z);
                transform.Forward = RotationToForward(actor.Rotation);
                dirty.Add(entity.Id);
            }

            if (authoritativeActorIds != null)
            {
                RemoveActorsMissingFromFullSnapshot(
                    world,
                    lookup,
                    authoritativeActorIds,
                    localActorId);
            }
        }

        private static void RemoveActorsMissingFromFullSnapshot(
            IECWorld world,
            BattleEntityLookup lookup,
            HashSet<int> authoritativeActorIds,
            int localActorId)
        {
            var staleEntities = new List<IEntityId>();
            world.ForEachAlive(entity =>
            {
                if (!entity.TryGetRef(out BattleNetIdComponent netId) || netId == null) return;
                if (netId.NetId.Value == localActorId) return;
                if (authoritativeActorIds.Contains(netId.NetId.Value)) return;

                staleEntities.Add(entity.Id);
            });

            for (int i = 0; i < staleEntities.Count; i++)
            {
                var entityId = staleEntities[i];
                lookup.UnbindByEntityId(entityId);
                if (world.IsAlive(entityId))
                {
                    world.Wrap(entityId).Destroy();
                }
            }
        }

        // Yaw → forward vector (右手系 Y-up, Forward=(0,0,1))
        private static Vector3 RotationToForward(float yaw)
        {
            return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }
    }
}
