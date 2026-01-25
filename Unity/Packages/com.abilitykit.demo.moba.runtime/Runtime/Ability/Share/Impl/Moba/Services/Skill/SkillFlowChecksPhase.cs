using System;
using AbilityKit.Ability;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillFlowChecksPhase : AbilityInstantPhaseBase
    {
        private readonly SkillChecksPhaseDTO _def;

        public SkillFlowChecksPhase(AbilityPipelinePhaseId phaseId, SkillChecksPhaseDTO def)
            : base(phaseId)
        {
            _def = def;
        }

        protected override void OnInstantExecute(IAbilityPipelineContext context)
        {
            if (context == null) return;

            // Placeholder for extensible checks.
            // For now, do not block execution until cooldown/state/tag modules are integrated.
            // When a check fails in the future:
            // - context.SetData(MobaSkillPipelineSharedKeys.FailReason, "...");
            // - context.IsAborted = true;

            _ = _def;
        }
    }
}
