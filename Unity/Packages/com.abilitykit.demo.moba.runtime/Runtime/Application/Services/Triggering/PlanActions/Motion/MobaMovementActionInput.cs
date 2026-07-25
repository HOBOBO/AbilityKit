using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 移动专用的动作输入。
    /// 该类型组合核心动作输入，并把移动方向、角色位置推导逻辑从通用动作输入中剥离出来。
    /// </summary>
    internal readonly struct MobaMovementActionInput
    {
        public MobaMovementActionInput(MobaPlanActionInput actionInput, MobaActorRegistry actors)
        {
            ActionInput = actionInput;
            Actors = actors;
        }

        public MobaPlanActionInput ActionInput { get; }
        public MobaActorRegistry Actors { get; }

        public int CasterActorId => ActionInput.CasterActorId;
        public int TargetActorId => ActionInput.TargetActorId;
        public bool HasCasterActor => ActionInput.HasCasterActor;
        public bool HasTargetActor => ActionInput.HasTargetActor;
        public bool HasActorRegistry => Actors != null;

        public int ResolveActorId(bool applyToCaster)
        {
            return applyToCaster ? CasterActorId : TargetActorId;
        }

        public bool TryGetAimDirection(out Vec3 direction)
        {
            direction = Vec3.Zero;
            if (!ActionInput.HasAimDirection || ActionInput.AimDirection.SqrMagnitude <= 0f)
            {
                return false;
            }

            direction = FlattenDirection(ActionInput.AimDirection);
            return direction.SqrMagnitude > 0f;
        }

        public bool TryGetDirectionToTarget(int selfActorId, out Vec3 direction)
        {
            direction = Vec3.Zero;
            if (Actors == null || TargetActorId <= 0 || TargetActorId == selfActorId)
            {
                return false;
            }

            if (!Actors.TryGet(selfActorId, out var self) || self == null || !self.hasTransform)
            {
                return false;
            }

            if (!Actors.TryGet(TargetActorId, out var target) || target == null || !target.hasTransform)
            {
                return false;
            }

            var delta = target.transform.Value.Position - self.transform.Value.Position;
            direction = FlattenDirection(delta);
            return direction.SqrMagnitude > 0f;
        }

        public bool TryGetDirectionToCaster(int selfActorId, out Vec3 direction)
        {
            direction = Vec3.Zero;
            if (Actors == null || CasterActorId <= 0 || CasterActorId == selfActorId)
            {
                return false;
            }

            if (!Actors.TryGet(CasterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                return false;
            }

            if (!Actors.TryGet(selfActorId, out var self) || self == null || !self.hasTransform)
            {
                return false;
            }

            var delta = caster.transform.Value.Position - self.transform.Value.Position;
            if (delta.SqrMagnitude > 0.01f)
            {
                direction = delta.Normalized;
                return true;
            }

            direction = Vec3.Forward;
            return true;
        }

        public bool TryGetBackwardFromActorForward(int actorId, out Vec3 direction)
        {
            direction = Vec3.Zero;
            if (Actors == null || !Actors.TryGet(actorId, out var actor) || actor == null || !actor.hasTransform)
            {
                return false;
            }

            direction = FlattenDirection(new Vec3(-actor.transform.Value.Forward.X, 0f, -actor.transform.Value.Forward.Z));
            return direction.SqrMagnitude > 0f;
        }

        public bool TryGetPlanarDeltaToAimPosition(int actorId, out Vec3 delta)
        {
            delta = Vec3.Zero;
            if (!ActionInput.HasAimPosition || Actors == null)
            {
                return false;
            }

            if (!Actors.TryGet(actorId, out var actor) || actor == null || !actor.hasTransform)
            {
                return false;
            }

            var position = actor.transform.Value.Position;
            delta = new Vec3(ActionInput.AimPosition.X - position.X, 0f, ActionInput.AimPosition.Z - position.Z);
            return delta.SqrMagnitude > 0f;
        }

        public Vec3 ResolveDashOrBlinkDirection(int directionMode, int selfActorId)
        {
            if (directionMode == 0)
            {
                if (TryGetAimDirection(out var aimDirection))
                {
                    return aimDirection;
                }

                if (TryGetPlanarDeltaToAimPosition(selfActorId, out var aimDelta))
                {
                    return aimDelta.Normalized;
                }

                return Vec3.Zero;
            }

            if (directionMode == 1)
            {
                return TryGetDirectionToTarget(selfActorId, out var targetDirection) ? targetDirection : Vec3.Zero;
            }

            return Vec3.Zero;
        }

        public Vec3 ResolvePullDirection(int directionMode, int targetActorId)
        {
            if (directionMode == 0)
            {
                return TryGetDirectionToCaster(targetActorId, out var casterDirection) ? casterDirection : Vec3.Zero;
            }

            if (directionMode == 1)
            {
                return TryGetBackwardFromActorForward(targetActorId, out var backwardDirection) ? backwardDirection : Vec3.Zero;
            }

            if (directionMode == 2)
            {
                return Vec3.Up;
            }

            if (directionMode == 3)
            {
                return TryGetDirectionToCaster(targetActorId, out var casterDirection)
                    ? -casterDirection
                    : Vec3.Zero;
            }

            return Vec3.Zero;
        }

        private static Vec3 FlattenDirection(Vec3 direction)
        {
            var flattened = new Vec3(direction.X, 0f, direction.Z);
            return flattened.SqrMagnitude > 0f ? flattened.Normalized : Vec3.Zero;
        }
    }
}
