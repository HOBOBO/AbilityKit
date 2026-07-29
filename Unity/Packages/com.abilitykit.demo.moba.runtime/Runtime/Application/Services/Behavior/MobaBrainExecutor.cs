using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    public sealed class MobaBrainExecutor : DefaultExecutor
    {
        public const string SkillCastEventId = "moba.brain.skill.cast";
        public const string SkillIdParam = "SkillId";
        public const string SkillSlotParam = "SkillSlot";
        public const string TargetActorIdParam = "TargetActorId";
        public const string AimPositionParam = "AimPosition";
        public const string AimDirectionParam = "AimDirection";

        public override void Execute(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
        {
            base.Execute(decision, context, output);

            var slot = decision.GetParam<int>(SkillSlotParam);
            if (slot <= 0) return;

            output.AddEvent(SkillCastEventId, new Dictionary<string, object>
            {
                [SkillIdParam] = decision.GetParam<int>(SkillIdParam),
                [SkillSlotParam] = slot,
                [TargetActorIdParam] = decision.GetParam<int>(TargetActorIdParam),
                [AimPositionParam] = decision.GetParam(AimPositionParam, Vec3.Zero),
                [AimDirectionParam] = decision.GetParam(AimDirectionParam, Vec3.Forward),
            });
        }
    }
}
