using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillRulePlanPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillRulePlanPhaseDTO _def;
        private readonly MobaTriggerPlanExecutor _executor;

        public SkillRulePlanPhase(AbilityPipelinePhaseId phaseId, SkillRulePlanPhaseDTO def, MobaTriggerPlanExecutor executor)
            : base(phaseId)
        {
            _def = def;
            _executor = executor;
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (context == null || _def == null || _def.TriggerIds == null || _def.TriggerIds.Length == 0) return;

            for (int i = 0; i < _def.TriggerIds.Length; i++)
            {
                var triggerId = _def.TriggerIds[i];
                if (triggerId <= 0) continue;

                if (_executor == null)
                {
                    Log.Warning($"[SkillRulePlanPhase] Rule plan executor missing. phase={PhaseId.Value}, triggerId={triggerId}, skillId={context.SkillId}, caster={context.CasterActorId}");
                }

                var transaction = MobaSkillCommitTransaction.Capture(context);
                MobaCombatTransactionPipeline transactions = null;
                var ok = context.WorldServices != null &&
                         context.WorldServices.TryResolve(out transactions) &&
                         transactions != null
                    ? transactions.TryExecute(
                        transaction,
                        _ => true,
                        _ => _executor != null && _executor.ExecuteRulePlan(triggerId, context))
                    : ExecuteWithoutPipeline(transaction, triggerId, context);

                if (ok) continue;
                if (!_def.AbortOnFailure) continue;

                context.FailReason = !string.IsNullOrEmpty(_def.FailReason)
                    ? _def.FailReason
                    : $"Skill rule plan failed: {triggerId}";
                context.IsAborted = true;
                return;
            }
        }

        private bool ExecuteWithoutPipeline(
            MobaSkillCommitTransaction transaction,
            int triggerId,
            SkillPipelineContext context)
        {
            var succeeded = false;
            try
            {
                succeeded = _executor != null && _executor.ExecuteRulePlan(triggerId, context);
                return succeeded;
            }
            finally
            {
                if (!succeeded) transaction.Rollback();
            }
        }

    }
}
