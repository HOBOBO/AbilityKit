using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class SkillRulePlanPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private const int DefaultSkillCommitTriggerId = 900101012;

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

                var transaction = SkillCommitTransaction.Capture(triggerId, context);
                var ok = false;
                try
                {
                    ok = _executor != null && _executor.ExecuteRulePlan(triggerId, context);
                }
                finally
                {
                    if (!ok) transaction.Rollback();
                }

                if (ok) continue;
                if (!_def.AbortOnFailure) continue;

                context.FailReason = !string.IsNullOrEmpty(_def.FailReason)
                    ? _def.FailReason
                    : $"Skill rule plan failed: {triggerId}";
                context.IsAborted = true;
                return;
            }
        }

        private readonly struct SkillCommitTransaction
        {
            private readonly ResourceState _resource;
            private readonly float _resourceCurrent;
            private readonly ActiveSkillRuntime _skill;
            private readonly int _cooldownDurationMs;
            private readonly long _cooldownEndTimeMs;

            private SkillCommitTransaction(
                ResourceState resource,
                ActiveSkillRuntime skill)
            {
                _resource = resource;
                _resourceCurrent = resource?.Current ?? 0f;
                _skill = skill;
                _cooldownDurationMs = skill?.CooldownDurationMs ?? 0;
                _cooldownEndTimeMs = skill?.CooldownEndTimeMs ?? 0L;
            }

            public static SkillCommitTransaction Capture(
                int triggerId,
                SkillPipelineContext context)
            {
                if (triggerId != DefaultSkillCommitTriggerId ||
                    context == null ||
                    context.WorldServices == null ||
                    !context.WorldServices.TryResolve<MobaActorLookupService>(out var actors) ||
                    actors == null)
                {
                    return default;
                }

                ResourceState resource = null;
                if (actors.TryGetActorEntity(context.CasterActorId, out var actor) &&
                    actor != null &&
                    actor.hasResourceContainer &&
                    actor.resourceContainer.Value?.Map != null)
                {
                    actor.resourceContainer.Value.Map.TryGetValue(
                        context.ResolvedConfiguration.ResourceType,
                        out resource);
                }

                MobaSkillRuntimeAccess.TryGetActiveSkill(
                    actors,
                    context.CasterActorId,
                    context.SkillSlot,
                    context.SkillId,
                    out var skill);
                return new SkillCommitTransaction(resource, skill);
            }

            public void Rollback()
            {
                if (_resource != null) _resource.Current = _resourceCurrent;
                if (_skill == null) return;

                _skill.CooldownDurationMs = _cooldownDurationMs;
                _skill.CooldownEndTimeMs = _cooldownEndTimeMs;
            }
        }
    }
}
