using System;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillPreCastCheckPhase : AbilityInstantPhaseBase
    {
        private readonly Func<IAbilityPipelineContext, bool> _checker;
        private readonly string _failReason;

        public SkillPreCastCheckPhase(AbilityPipelinePhaseId phaseId, Func<IAbilityPipelineContext, bool> checker, string failReason = null)
            : base(phaseId)
        {
            _checker = checker;
            _failReason = failReason;
        }

        protected override void OnInstantExecute(IAbilityPipelineContext context)
        {
            if (_checker == null) return;
            if (_checker(context)) return;

            if (!string.IsNullOrEmpty(_failReason))
            {
                context.SetData(MobaSkillPipelineSharedKeys.FailReason, _failReason);
            }

            context.IsAborted = true;
        }
    }
}
