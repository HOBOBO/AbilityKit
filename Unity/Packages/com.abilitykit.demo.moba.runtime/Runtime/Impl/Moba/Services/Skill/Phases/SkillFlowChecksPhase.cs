using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillFlowChecksPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillChecksPhaseDTO _def;

        public SkillFlowChecksPhase(AbilityPipelinePhaseId phaseId, SkillChecksPhaseDTO def)
            : base(phaseId)
        {
            _def = def;
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (context == null) return;

            // Placeholder for extensible checks.
            // For now, do not block execution until cooldown/state/tag modules are integrated.
            // When a check fails in the future:
            // - context.FailReason = "...";
            // - context.IsAborted = true;

            _ = _def;
        }
    }
}
