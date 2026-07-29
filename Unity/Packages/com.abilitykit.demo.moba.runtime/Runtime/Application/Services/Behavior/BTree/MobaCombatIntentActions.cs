using System;
using System.Globalization;
using BTCore.Runtime;
using BTCore.Runtime.Externals;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    public sealed class MobaResolveTargetAimAction : ExternalAction
    {
        protected override NodeState OnUpdate()
        {
            if (Blackboard == null || !Blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid))
                return NodeState.Failure;

            var dx = Blackboard.GetValue<float>(MobaBTreeKeys.TargetX)
                     - Blackboard.GetValue<float>(MobaBTreeKeys.OwnerX);
            var dy = Blackboard.GetValue<float>(MobaBTreeKeys.TargetY)
                     - Blackboard.GetValue<float>(MobaBTreeKeys.OwnerY);
            var dz = Blackboard.GetValue<float>(MobaBTreeKeys.TargetZ)
                     - Blackboard.GetValue<float>(MobaBTreeKeys.OwnerZ);
            var length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            Blackboard.SetValue(MobaBTreeKeys.AimTargetActorId,
                Blackboard.GetValue<int>(MobaBTreeKeys.TargetId));
            Blackboard.SetValue(MobaBTreeKeys.AimX,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetX));
            Blackboard.SetValue(MobaBTreeKeys.AimY,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetY));
            Blackboard.SetValue(MobaBTreeKeys.AimZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetZ));
            Blackboard.SetValue(MobaBTreeKeys.AimDirectionX, length > 0.0001f ? dx / length : 0f);
            Blackboard.SetValue(MobaBTreeKeys.AimDirectionY, length > 0.0001f ? dy / length : 0f);
            Blackboard.SetValue(MobaBTreeKeys.AimDirectionZ, length > 0.0001f ? dz / length : 1f);
            Blackboard.SetValue(MobaBTreeKeys.AimValid, true);
            return NodeState.Success;
        }
    }

    public sealed class MobaCastSelectedSkillAction : ExternalAction
    {
        private const int DefaultPriority = 100;

        protected override NodeState OnUpdate()
        {
            if (Blackboard == null
                || !Blackboard.GetValue<bool>(MobaBTreeKeys.SkillValid)
                || !Blackboard.GetValue<bool>(MobaBTreeKeys.AimValid))
                return NodeState.Failure;

            Blackboard.SetValue(MobaBTreeKeys.CastRequestPriority, ReadPriority(DefaultPriority));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestSkillId,
                Blackboard.GetValue<int>(MobaBTreeKeys.SkillId));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestSkillSlot,
                Blackboard.GetValue<int>(MobaBTreeKeys.SkillSlot));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestTargetActorId,
                Blackboard.GetValue<int>(MobaBTreeKeys.AimTargetActorId));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestAimX,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimX));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestAimY,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimY));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestAimZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimZ));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestDirectionX,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimDirectionX));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestDirectionY,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimDirectionY));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestDirectionZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.AimDirectionZ));
            Blackboard.SetValue(MobaBTreeKeys.CastRequestValid, true);
            return NodeState.Success;
        }

        private int ReadPriority(int defaultValue)
        {
            return Properties != null
                   && Properties.TryGetValue("priority", out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }

    public sealed class MobaMoveToEnemyAction : ExternalAction
    {
        private const int DefaultPriority = 50;

        protected override NodeState OnUpdate()
        {
            if (Blackboard == null || !Blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid))
                return NodeState.Failure;

            Blackboard.SetValue(MobaBTreeKeys.MoveRequestPriority, ReadPriority(DefaultPriority));
            Blackboard.SetValue(MobaBTreeKeys.MoveRequestX,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetX));
            Blackboard.SetValue(MobaBTreeKeys.MoveRequestY,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetY));
            Blackboard.SetValue(MobaBTreeKeys.MoveRequestZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.TargetZ));
            Blackboard.SetValue(MobaBTreeKeys.MoveRequestStopRange,
                Blackboard.GetValue<float>(MobaBTreeKeys.SkillApproachRange));
            Blackboard.SetValue(MobaBTreeKeys.MoveRequestValid, true);
            return NodeState.Success;
        }

        private int ReadPriority(int defaultValue)
        {
            return Properties != null
                   && Properties.TryGetValue("priority", out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }

    public sealed class MobaHoldPositionAction : ExternalAction
    {
        private const int DefaultPriority = 0;

        protected override NodeState OnUpdate()
        {
            if (Blackboard == null) return NodeState.Failure;
            Blackboard.SetValue(MobaBTreeKeys.HoldRequestPriority, ReadPriority(DefaultPriority));
            Blackboard.SetValue(MobaBTreeKeys.HoldRequestValid, true);
            return NodeState.Success;
        }

        private int ReadPriority(int defaultValue)
        {
            return Properties != null
                   && Properties.TryGetValue("priority", out var value)
                   && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : defaultValue;
        }
    }

    /// <summary>
    /// The only node allowed to publish out.*. Candidate branches can be combined or run in
    /// parallel without relying on their execution order to resolve conflicts.
    /// </summary>
    public sealed class MobaArbitrateCombatIntentAction : ExternalAction
    {
        protected override NodeState OnUpdate()
        {
            if (Blackboard == null) return NodeState.Failure;

            Blackboard.SetValue(MobaBTreeKeys.OutputKind, (int)MobaBTreeIntentKind.Hold);
            Blackboard.SetValue(MobaBTreeKeys.HasMove, false);
            Blackboard.SetValue(MobaBTreeKeys.HasCast, false);

            var hasCast = Blackboard.GetValue<bool>(MobaBTreeKeys.CastRequestValid);
            var hasMove = Blackboard.GetValue<bool>(MobaBTreeKeys.MoveRequestValid);
            var hasHold = Blackboard.GetValue<bool>(MobaBTreeKeys.HoldRequestValid);
            var castPriority = hasCast
                ? Blackboard.GetValue<int>(MobaBTreeKeys.CastRequestPriority)
                : int.MinValue;
            var movePriority = hasMove
                ? Blackboard.GetValue<int>(MobaBTreeKeys.MoveRequestPriority)
                : int.MinValue;
            var holdPriority = hasHold
                ? Blackboard.GetValue<int>(MobaBTreeKeys.HoldRequestPriority)
                : int.MinValue;

            if (hasCast && castPriority >= movePriority && castPriority >= holdPriority)
            {
                PublishCast();
            }
            else if (hasMove && movePriority >= holdPriority)
            {
                PublishMove();
            }

            return NodeState.Success;
        }

        private void PublishCast()
        {
            Blackboard.SetValue(MobaBTreeKeys.OutputKind, (int)MobaBTreeIntentKind.Cast);
            Blackboard.SetValue(MobaBTreeKeys.HasCast, true);
            Blackboard.SetValue(MobaBTreeKeys.CastSkillId,
                Blackboard.GetValue<int>(MobaBTreeKeys.CastRequestSkillId));
            Blackboard.SetValue(MobaBTreeKeys.CastSkillSlot,
                Blackboard.GetValue<int>(MobaBTreeKeys.CastRequestSkillSlot));
            Blackboard.SetValue(MobaBTreeKeys.CastTargetActorId,
                Blackboard.GetValue<int>(MobaBTreeKeys.CastRequestTargetActorId));
            Blackboard.SetValue(MobaBTreeKeys.CastAimX,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestAimX));
            Blackboard.SetValue(MobaBTreeKeys.CastAimY,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestAimY));
            Blackboard.SetValue(MobaBTreeKeys.CastAimZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestAimZ));
            Blackboard.SetValue(MobaBTreeKeys.CastDirectionX,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestDirectionX));
            Blackboard.SetValue(MobaBTreeKeys.CastDirectionY,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestDirectionY));
            Blackboard.SetValue(MobaBTreeKeys.CastDirectionZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.CastRequestDirectionZ));
        }

        private void PublishMove()
        {
            Blackboard.SetValue(MobaBTreeKeys.OutputKind, (int)MobaBTreeIntentKind.Move);
            Blackboard.SetValue(MobaBTreeKeys.HasMove, true);
            Blackboard.SetValue(MobaBTreeKeys.MoveX,
                Blackboard.GetValue<float>(MobaBTreeKeys.MoveRequestX));
            Blackboard.SetValue(MobaBTreeKeys.MoveY,
                Blackboard.GetValue<float>(MobaBTreeKeys.MoveRequestY));
            Blackboard.SetValue(MobaBTreeKeys.MoveZ,
                Blackboard.GetValue<float>(MobaBTreeKeys.MoveRequestZ));
        }
    }
}
