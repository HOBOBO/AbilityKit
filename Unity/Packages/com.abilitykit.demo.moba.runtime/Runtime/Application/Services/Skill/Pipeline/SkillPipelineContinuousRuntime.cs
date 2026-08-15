using System;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillPipelineContinuousRuntime : MobaContinuousRuntimeBase
    {
        private readonly SkillPipelineContinuousConfig _config;

        public SkillPipelineContinuousRuntime(SkillPipelineContext context, MobaSkillPipelineConfig pipelineConfig, ContinuousTagRequirements requirements)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            PipelineConfig = pipelineConfig ?? throw new ArgumentNullException(nameof(pipelineConfig));
            ModifierSourceId = CreateModifierSourceId(context.SourceContextId, context.SkillId, context.CasterActorId, pipelineConfig.ConfigId);
            _config = new SkillPipelineContinuousConfig(this, requirements ?? new ContinuousTagRequirements());
        }

        public SkillPipelineContext Context { get; }
        public MobaSkillPipelineConfig PipelineConfig { get; }
        public int SkillId => Context.SkillId;
        public int CasterActorId => Context.CasterActorId;
        public int TargetActorId => Context.TargetActorId;
        public int ConfigId => PipelineConfig.ConfigId;
        public long SourceContextId => Context.SourceContextId;
        public int ModifierSourceId { get; }
        public override IContinuousConfig Config => _config;

        protected override bool OnActivating()
        {
            return Context.CasterActorId > 0 && PipelineConfig.HasPipelineContinuousTagTemplate;
        }

        private static int CreateModifierSourceId(long sourceContextId, int skillId, int casterActorId, int configId)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + skillId;
                hash = hash * 31 + casterActorId;
                hash = hash * 31 + configId;
                return hash == 0 ? skillId : hash;
            }
        }

        private sealed class SkillPipelineContinuousConfig : MobaContinuousConfigBase
        {
            private readonly SkillPipelineContinuousRuntime _runtime;

            public SkillPipelineContinuousConfig(SkillPipelineContinuousRuntime runtime, ContinuousTagRequirements requirements)
                : base(0f, requirements, Array.Empty<IMobaContinuousModifierSpec>())
            {
                _runtime = runtime;
            }

            public override string Id => $"skill_pipeline:{_runtime.CasterActorId}:{_runtime.SkillId}:{_runtime.ConfigId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.CasterActorId;
            public override int OwnerActorId => _runtime.CasterActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
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
