using System;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillEconomyPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillEconomyPhaseDTO _specification;

        public SkillEconomyPhase(AbilityPipelinePhaseId phaseId, SkillEconomyPhaseDTO specification)
            : base(phaseId)
        {
            _specification = specification ?? throw new ArgumentNullException(nameof(specification));
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (context?.WorldServices == null ||
                !context.WorldServices.TryResolve<MobaSkillEconomyService>(out var economy) || economy == null)
            {
                Fail(context, "Skill economy service is unavailable.");
                return;
            }

            string failure;
            var succeeded = (SkillEconomyOperation)_specification.Operation switch
            {
                SkillEconomyOperation.ReserveCast => economy.TryReserve(context, _specification, out failure),
                SkillEconomyOperation.ConsumeResource => economy.TryConsumeResource(context, _specification, out failure),
                _ => FailUnsupported(out failure),
            };
            if (!succeeded) Fail(context, failure);
        }

        private bool FailUnsupported(out string failure)
        {
            failure = $"Unsupported skill economy operation: {_specification.Operation}.";
            return false;
        }

        private void Fail(SkillPipelineContext context, string failure)
        {
            if (context == null) return;
            context.FailReason = string.IsNullOrWhiteSpace(_specification.FailReason)
                ? failure
                : _specification.FailReason;
            context.IsAborted = true;
        }
    }
}
