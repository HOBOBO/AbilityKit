using BTCore.Runtime.Externals;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    public sealed class MobaHasEnemyCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            return Blackboard != null && Blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid);
        }
    }

    public sealed class MobaHasSelectedSkillCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            return Blackboard != null && Blackboard.GetValue<bool>(MobaBTreeKeys.SkillValid);
        }
    }

    public sealed class MobaCanCastCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            return Blackboard != null && Blackboard.GetValue<bool>(MobaBTreeKeys.OwnerCanCast);
        }
    }

    public sealed class MobaCanMoveCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            return Blackboard != null && Blackboard.GetValue<bool>(MobaBTreeKeys.OwnerCanMove);
        }
    }

    public sealed class MobaSelectedSkillInRangeCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            if (Blackboard == null
                || !Blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid)
                || !Blackboard.GetValue<bool>(MobaBTreeKeys.SkillValid))
                return false;

            var range = Blackboard.GetValue<float>(MobaBTreeKeys.SkillRange);
            return range > 0f && Blackboard.GetValue<float>(MobaBTreeKeys.TargetDistance) <= range;
        }
    }

    public sealed class MobaShouldApproachEnemyCondition : ExternalCondition
    {
        protected override bool Validate()
        {
            if (Blackboard == null || !Blackboard.GetValue<bool>(MobaBTreeKeys.TargetValid)) return false;
            var range = Blackboard.GetValue<float>(MobaBTreeKeys.SkillApproachRange);
            if (range <= 0f) range = MobaSelectReadySkillAction.DefaultApproachRange;
            return Blackboard.GetValue<float>(MobaBTreeKeys.TargetDistance) > range;
        }
    }
}
