using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillPipelineContinuousRuntime : MobaContinuousRuntimeBase
    {
        private readonly SkillPipelineContinuousConfig _config;
        private readonly ContinuousTagRequirements _baseRequirements;
        private Dictionary<SkillTimelinePhase, GameplayTagContainer> _timelineGrants;

        public SkillPipelineContinuousRuntime(SkillPipelineContext context, MobaSkillPipelineConfig pipelineConfig, ContinuousTagRequirements requirements,
            ulong ownerExecutionBlockedResultMask = 0UL)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            PipelineConfig = pipelineConfig ?? throw new ArgumentNullException(nameof(pipelineConfig));
            _baseRequirements = requirements ?? new ContinuousTagRequirements();
            _config = new SkillPipelineContinuousConfig(this, _baseRequirements, ownerExecutionBlockedResultMask);
        }

        public SkillPipelineContext Context { get; }
        public MobaSkillPipelineConfig PipelineConfig { get; }
        public int SkillId => Context.SkillId;
        public int CasterActorId => Context.CasterActorId;
        public int TargetActorId => Context.TargetActorId;
        public int ConfigId => PipelineConfig.ConfigId;
        public long SourceContextId => Context.SourceContextId;
        public int ModifierSourceId => AllocatedModifierSourceId;
        public override IContinuousConfig Config => _config;

        internal void SetTimelineGrant(SkillTimelinePhase phase, GameplayTagContainer grant, bool restoring)
        {
            if (grant == null)
            {
                if (_timelineGrants == null || !_timelineGrants.Remove(phase)) return;
            }
            else
            {
                _timelineGrants ??= new Dictionary<SkillTimelinePhase, GameplayTagContainer>();
                _timelineGrants[phase] = grant;
            }

            if (_timelineGrants.Count == 0)
                _config.TagRequirements = _baseRequirements;
            else
            {
                var tags = new GameplayTagContainer();
                tags.AppendTags(_baseRequirements.ApplicationTags);
                foreach (var contribution in _timelineGrants.Values) tags.AppendTags(contribution);
                _config.TagRequirements = new ContinuousTagRequirements
                {
                    ActivationRequired = _baseRequirements.ActivationRequired,
                    RemovalRequired = _baseRequirements.RemovalRequired,
                    OngoingRequired = _baseRequirements.OngoingRequired,
                    ExecutionBlockedResultMask = _baseRequirements.ExecutionBlockedResultMask,
                    ApplicationTags = tags,
                };
            }

            if (restoring)
            {
                if (Context.WorldServices.TryResolve<IMobaEffectiveTagQueryService>(out var effectiveTags))
                    effectiveTags.MarkDirty(CasterActorId);
            }
            else if (Context.WorldServices.TryResolve<MobaContinuousManager>(out var manager))
                manager.RefreshTagRequirements(this);
        }

        protected override bool OnActivating()
        {
            return Context.CasterActorId > 0;
        }

        private sealed class SkillPipelineContinuousConfig : MobaContinuousConfigBase, IMobaContinuousOwnerExecutionGateConfig
        {
            private readonly SkillPipelineContinuousRuntime _runtime;

            public SkillPipelineContinuousConfig(SkillPipelineContinuousRuntime runtime, ContinuousTagRequirements requirements,
                ulong ownerExecutionBlockedResultMask)
                : base(0f, requirements, Array.Empty<IMobaContinuousModifierSpec>())
            {
                _runtime = runtime;
                OwnerExecutionBlockedResultMask = ownerExecutionBlockedResultMask;
            }

            public override string Id => $"skill_pipeline:{_runtime.CasterActorId}:{_runtime.SkillId}:{_runtime.ConfigId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.CasterActorId;
            public override int OwnerActorId => _runtime.CasterActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public ulong OwnerExecutionBlockedResultMask { get; }
            public override GameplayTagSource TagSource => CreateSource(_runtime);

            private static GameplayTagSource CreateSource(SkillPipelineContinuousRuntime runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.CasterActorId != 0) return new GameplayTagSource(runtime.CasterActorId);
                return GameplayTagSource.System;
            }
        }
    }
}
