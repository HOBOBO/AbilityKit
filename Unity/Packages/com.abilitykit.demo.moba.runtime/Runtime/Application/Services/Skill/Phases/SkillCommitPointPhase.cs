using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillCommitPointPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillCommitPointPhaseDTO _specification;

        public SkillCommitPointPhase(AbilityPipelinePhaseId phaseId, SkillCommitPointPhaseDTO specification)
            : base(phaseId)
        {
            _specification = specification;
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (context?.WorldServices == null || !context.TryGetSkillRuntimeHandle(out var handle)) return;
            if (context.WorldServices.TryResolve<MobaSkillEconomyService>(out var economy) && economy != null)
            {
                economy.Commit(in handle, _specification?.CommitId);
            }
            if (context.WorldServices.TryResolve<MobaSkillWindowRuntimeService>(out var windows) && windows != null)
            {
                windows.Commit(in handle, _specification?.CommitId);
            }
        }
    }
}
