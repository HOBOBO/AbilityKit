using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// Code 驱动决策工厂：把目录中的决策名映射为具体 <see cref="IBehaviorDecision"/>。
    ///
    /// - "idle"：空转
    /// - "patrol"：以出生时位置为中心的两点往返巡逻
    /// - "chase"：追击最近的敌对 actor（每帧重选目标）
    /// </summary>
    internal static class MobaBrainDecisionFactory
    {
        public static IBehaviorDecision Create(
            in MobaActorBrainDefinition definition,
            MobaActorRegistry registry,
            long ownerActorId)
        {
            switch (definition.DecisionName)
            {
                case "patrol":
                    return CreatePatrolAroundSpawn(registry, ownerActorId, definition.Param0);
                case "chase":
                    return CreateNearestEnemyChase(registry, ownerActorId, definition.Param0);
                default:
                    return new IdleDecision();
            }
        }

        private static IBehaviorDecision CreatePatrolAroundSpawn(
            MobaActorRegistry registry,
            long ownerActorId,
            float stopDistance)
        {
            var waypoints = ResolvePatrolWaypoints(registry, ownerActorId);
            return MobaBehaviorDecisions.CreatePatrolDecision(
                waypoints,
                stopDistance: stopDistance > 0f ? stopDistance : 0.5f);
        }

        private static IBehaviorDecision CreateNearestEnemyChase(
            MobaActorRegistry registry,
            long ownerActorId,
            float attackRange)
        {
            var range = attackRange > 0f ? attackRange : 1.5f;

            return new DelegateDecision("NearestEnemyChase", (ctx, world) =>
            {
                if (!world.CanMove(ctx.OwnerId))
                    return DecisionResult.Continue("Chase");

                var targetId = FindNearestEnemy(registry, world, ctx.OwnerId);
                if (targetId <= 0)
                    return DecisionResult.Continue("Chase");

                var targetPos = world.GetPosition(new BehaviorEntityId(targetId));
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                if (distance <= range)
                    return DecisionResult.Continue("InRange");

                var speed = world.GetMoveSpeed(ctx.OwnerId, 5f);
                return DecisionResult.Continue("Chasing")
                    .WithMovement(targetPos, new BehaviorEntityId(targetId), speed);
            });
        }

        private static int FindNearestEnemy(
            MobaActorRegistry registry,
            IWorldQuery world,
            BehaviorEntityId ownerId)
        {
            if (registry == null || world == null) return 0;

            var ownerTeam = 0;
            if (world is MobaWorldQuery moba)
            {
                ownerTeam = moba.GetTeam(ownerId);
            }

            var ownerPos = world.GetPosition(ownerId);
            var bestId = 0;
            var bestDistSq = float.MaxValue;

            foreach (var kv in registry.Entries)
            {
                var e = kv.Value;
                if (e == null || !e.isEnabled || !e.hasTransform) continue;
                if (kv.Key == ownerId.Value) continue;

                if (e.hasTeam)
                {
                    var team = (int)e.team.Value;
                    if (ownerTeam != 0 && team != 0 && team == ownerTeam) continue;
                }

                if (e.hasAttributeGroup && e.attributeGroup.Group != null
                    && e.attributeGroup.Group.GetValue(Attributes.MobaAttributeIds.HP) <= 0f)
                {
                    continue;
                }

                var delta = e.transform.Value.Position - ownerPos;
                var distSq = delta.SqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestId = kv.Key;
                }
            }

            return bestId;
        }

        private static Vec3[] ResolvePatrolWaypoints(MobaActorRegistry registry, long ownerActorId)
        {
            if (registry != null && registry.TryGet((int)ownerActorId, out var e) && e != null && e.hasTransform)
            {
                var p = e.transform.Value.Position;
                return new[]
                {
                    new Vec3(p.X + 5f, p.Y, p.Z),
                    new Vec3(p.X - 5f, p.Y, p.Z),
                };
            }

            return new[] { Vec3.Zero };
        }

        private sealed class IdleDecision : IBehaviorDecision
        {
            public string DecisionType => "MobaActorBrainIdle";
            public string CurrentState => "Idle";

            public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
            {
                return DecisionResult.Continue(CurrentState);
            }
        }
    }
}
