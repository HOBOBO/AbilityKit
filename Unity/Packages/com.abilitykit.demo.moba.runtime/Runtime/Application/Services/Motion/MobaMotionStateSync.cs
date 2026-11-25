using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    /// <summary>
    /// 权威普通移动截面的逻辑值，不从表现速度推导持续输入。这里只描述采样点，
    /// 不证明之后的输入终结或重演区间的外部依赖已覆盖。
    /// </summary>
    public readonly struct MobaAuthoritativeMovementAnchor
    {
        public readonly int ActorId;
        public readonly int EntityVersion;
        public readonly Transform3 Pose;
        public readonly float MoveX;
        public readonly float MoveZ;
        public readonly float MoveSpeed;
        public readonly bool CanMove;
        public readonly bool HasIsolatedLocomotion;

        private MobaAuthoritativeMovementAnchor(int actorId, int entityVersion, Transform3 pose,
            float moveX, float moveZ, float moveSpeed, bool canMove, bool hasIsolatedLocomotion)
        {
            ActorId = actorId;
            EntityVersion = entityVersion;
            Pose = pose;
            MoveX = moveX;
            MoveZ = moveZ;
            MoveSpeed = moveSpeed;
            CanMove = canMove;
            HasIsolatedLocomotion = hasIsolatedLocomotion;
        }

        /// <summary>
        /// 读取当前控制关系和存活实例。调用方须把该值绑定到已完成的权威 Tick；
        /// 未初始化 Motion、碰撞求解器或额外 Motion 来源均不具备隔离移动资格。
        /// </summary>
        public static bool TryCapture(IWorldResolver services, PlayerId player, out MobaAuthoritativeMovementAnchor anchor)
        {
            anchor = default;
            if (services == null ||
                !services.TryResolve<MobaPlayerActorMapService>(out var players) || !players.TryGetActorId(player, out var actorId) ||
                !services.TryResolve<MobaActorRegistry>(out var actors) || !actors.TryGet(actorId, out var actor) ||
                !services.TryResolve<MobaCombatRulesService>(out var rules) || !rules.IsAlive(actorId) ||
                !actor.hasTransform || !actor.hasAttributeGroup || actor.attributeGroup.Group == null)
                return false;

            var version = actors.GetEntityVersion(actorId);
            if (version <= 0) return false;

            var moveX = actor.hasMoveInput ? actor.moveInput.Dx : 0f;
            var moveZ = actor.hasMoveInput ? actor.moveInput.Dz : 0f;
            var attributes = actor.attributeGroup;
            if (attributes.Ctx == null || !attributes.Group.TryGet(MobaAttributeIds.MOVE_SPEED, out _)) return false;
            var speed = attributes.Ctx.GetValueSilently(MobaAttributeIds.MOVE_SPEED);
            var pose = actor.transform.Value;
            if (!IsFinite(moveX) || !IsFinite(moveZ) || !IsFinite(speed) || speed < 0f ||
                !IsFinite(pose.Position.X) || !IsFinite(pose.Position.Y) || !IsFinite(pose.Position.Z) ||
                !IsFinite(pose.Rotation.X) || !IsFinite(pose.Rotation.Y) ||
                !IsFinite(pose.Rotation.Z) || !IsFinite(pose.Rotation.W)) return false;

            var canMove = rules.CanMove(actorId);
            var isolated = canMove && HasOnlyLocomotion(services, actor, actorId, version);
            anchor = new MobaAuthoritativeMovementAnchor(actorId, version, pose,
                moveX, moveZ, speed, canMove, isolated);
            return true;
        }

        private static bool HasOnlyLocomotion(IWorldResolver services, global::ActorEntity actor, int actorId, int version)
        {
            if (!actor.hasMotion || !actor.motion.Initialized || actor.motion.Pipeline == null) return false;
            var motion = actor.motion;
            var pipeline = motion.Pipeline;
            if (!ReferenceEquals(pipeline.Solver, NoMotionSolver.Instance) ||
                (motion.Solver != null && !ReferenceEquals(motion.Solver, NoMotionSolver.Instance))) return false;

            return pipeline.SourceCount == 0 ||
                (pipeline.SourceCount == 1 && actor.hasMoveInput &&
                 services.TryResolve<MobaLocomotionRuntimeService>(out var locomotion) &&
                 locomotion.TryGetBinding(actorId, actor, version, pipeline, out _));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal static class MobaMotionStateSync
    {
        internal static void SyncFromTransform(global::ActorEntity entity, in Transform3 transform)
        {
            if (entity == null || !entity.hasMotion) return;

            var motion = entity.motion;
            var state = motion.State;
            state.Position = transform.Position;
            state.Forward = transform.Forward;
            var output = motion.Output;
            output.NewForward = state.Forward;
            entity.ReplaceMotion(motion.Pipeline, state, output, motion.Solver, motion.Policy,
                motion.Events, motion.Initialized, motion.HitTriggerRuntime);
        }
    }
}
