using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaSkillCommitTransaction : MobaCombatTransactionBase, IMobaRevertibleCombatTransaction
    {
        private readonly ResourceState _resource;
        private readonly Fixed64 _resourceCurrent;
        private readonly ActiveSkillRuntime _skill;
        private readonly int _cooldownDurationMs;
        private readonly long _cooldownEndTimeMs;

        private MobaSkillCommitTransaction(
            long rootContextId,
            ResourceState resource,
            ActiveSkillRuntime skill)
            : base(rootContextId)
        {
            _resource = resource;
            _resourceCurrent = resource?.Current ?? Fixed64.Zero;
            _skill = skill;
            _cooldownDurationMs = skill?.CooldownDurationMs ?? 0;
            _cooldownEndTimeMs = skill?.CooldownEndTimeMs ?? 0L;
        }

        public static MobaSkillCommitTransaction Capture(SkillPipelineContext context)
        {
            if (context == null || context.WorldServices == null ||
                !context.WorldServices.TryResolve<MobaActorLookupService>(out var actors) || actors == null)
                return new MobaSkillCommitTransaction(context?.RuntimeHandle.RootTraceContextId ?? 0L, null, null);

            ResourceState resource = null;
            if (actors.TryGetActorEntity(context.CasterActorId, out var actor) && actor != null &&
                actor.hasResourceContainer && actor.resourceContainer.Value?.Map != null)
                actor.resourceContainer.Value.Map.TryGetValue(context.ResolvedConfiguration.ResourceType, out resource);

            MobaSkillRuntimeAccess.TryGetActiveSkill(
                actors,
                context.CasterActorId,
                context.SkillSlot,
                context.SkillId,
                out var skill);
            return new MobaSkillCommitTransaction(context.RuntimeHandle.RootTraceContextId, resource, skill);
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
