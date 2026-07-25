using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
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
    /// 本地玩家的 transform 不由此类覆盖——预测通道由 PredictionViewBridge 负责。
    /// </summary>
    internal static class BattleRemoteInterpolationApplier
    {
        /// <summary>
        /// 将插值后的 snapshot 应用到 view EntityWorld 中的远端实体。
        /// </summary>
        /// <param name="ctx">BattleContext（提供 view EntityWorld + EntityLookup）</param>
        /// <param name="snapshot">插值后的 actor 列表</param>
        /// <param name="localActorId">本地玩家 actorId（跳过，由 PredictionViewBridge 负责）</param>
        public static void Apply(BattleContext ctx, in GatewayStateSyncSnapshot snapshot, int localActorId)
        {
            if (ctx == null) return;

            var world = ctx.EntityWorld;
            var lookup = ctx.EntityLookup;
            if (world == null || lookup == null) return;

            var actors = snapshot.Actors;
            if (actors == null || actors.Length == 0) return;

            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor.ActorId <= 0 || actor.ActorId == localActorId) continue;

                if (!lookup.TryResolve(world, new BattleNetId(actor.ActorId), out var entity)) continue;

                if (entity.TryGetRef(out BattleTransformComponent transform) && transform != null)
                {
                    transform.Position = new Vector3(actor.X, actor.Y, actor.Z);
                    transform.Forward = RotationToForward(actor.Rotation);
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
