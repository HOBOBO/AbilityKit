using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// 将通用决策翻译为 MOBA 当前输出；派生执行器仍可用于普通业务。
    /// 回滚准入仅自动覆盖本标准实现。
    /// </summary>
    public class MobaBrainExecutor : DefaultExecutor, IBehaviorRecoveryDependency
    {
        /// <summary>
        /// 此执行器无自有跨帧状态，输出由 Runtime 保存，不在恢复时执行。
        /// </summary>
        public void ValidateRecoveryDependency()
        {
            if (GetType() != typeof(MobaBrainExecutor))
                throw new System.InvalidOperationException($"Brain executor '{GetType().FullName}' requires its own recovery dependency contract before enabling rollback.");
        }

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
