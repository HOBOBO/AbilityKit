using System;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillPreCastCheckPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly Func<SkillPipelineContext, bool> _checker;
        private readonly string _failReason;

        public SkillPreCastCheckPhase(AbilityPipelinePhaseId phaseId, Func<SkillPipelineContext, bool> checker, string failReason = null)
            : base(phaseId)
        {
            _checker = checker;
            _failReason = failReason;
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (_checker == null) return;
            if (_checker(context)) return;

            if (!string.IsNullOrEmpty(_failReason))
            {
                context.FailReason = _failReason;
            }

            context.IsAborted = true;
        }
    }
}
