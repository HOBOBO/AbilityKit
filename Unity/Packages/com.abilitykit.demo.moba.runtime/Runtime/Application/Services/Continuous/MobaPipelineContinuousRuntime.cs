using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 将长时间运行的 pipeline 封装为 continuous runtime，使生命周期、标签和 modifier 仍由 Continuous 持有。
    /// 领域逻辑保留在 pipeline 阶段内，或保留在这些阶段调用的领域 manager 中。
    /// </summary>
    public sealed class MobaPipelineContinuousRuntime<TCtx> : MobaContinuousRuntimeBase,
        IMobaTickableContinuous,
        IMobaContinuousRuntimeDebugSource,
        IMobaContinuousExecutionContextProvider
        where TCtx : IAbilityPipelineContext
    {
        private readonly MobaPipelineContinuousConfig _config;
        private readonly MobaContextSourceView _source;
        private readonly string _kind;

        public MobaPipelineContinuousRuntime(
            IAbilityPipelineRun<TCtx> run,
            IAbilityPipelineConfig pipelineConfig,
            int ownerActorId,
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            ContinuousTagRequirements requirements,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers = null,
            string kind = null,
            MobaContextSourceView source = default)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
            PipelineConfig = pipelineConfig ?? throw new ArgumentNullException(nameof(pipelineConfig));
            Context = run.Context;
            OwnerActorId = ownerActorId > 0 ? ownerActorId : ResolveSourceActorId(Context);
            SourceActorId = sourceActorId > 0 ? sourceActorId : ResolveSourceActorId(Context);
            TargetActorId = targetActorId > 0 ? targetActorId : ResolveTargetActorId(Context, SourceActorId);
            SourceContextId = sourceContextId != 0 ? sourceContextId : ResolveSourceContextId(Context);
            ModifierSourceId = CreateModifierSourceId(SourceContextId, PipelineConfig.ConfigId, OwnerActorId, SourceActorId, TargetActorId);
            _source = source;
            _kind = string.IsNullOrEmpty(kind) ? "PipelineContinuous" : kind;
            _config = new MobaPipelineContinuousConfig(this, requirements ?? new ContinuousTagRequirements(), modifiers);
        }

        public IAbilityPipelineRun<TCtx> Run { get; }
        public IAbilityPipelineConfig PipelineConfig { get; }
        public TCtx Context { get; }
        public int ConfigId => PipelineConfig.ConfigId;
        public string ConfigName => PipelineConfig.ConfigName;
        public int OwnerActorId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public int ModifierSourceId { get; }
        public EAbilityPipelineState PipelineState => Run.State;
        public AbilityPipelinePhaseId CurrentPhaseId => Run.CurrentPhaseId;
        public override IContinuousConfig Config => _config;

        public float RemainingSeconds => float.PositiveInfinity;

        protected override bool OnActivating()
        {
            return Run != null && Context != null && OwnerActorId > 0 && SourceActorId > 0;
        }

        protected override void OnPaused()
        {
            Run.Pause();
        }

        protected override void OnResumed()
        {
            Run.Resume();
        }

        protected override void OnEnding(ContinuousEndReason reason)
        {
            if (Run.State == EAbilityPipelineState.Executing)
            {
                if (reason == ContinuousEndReason.Completed)
                {
                    Run.Cancel();
                }
                else
                {
                    Run.Interrupt();
                }
            }
        }

        public void TickManaged(float deltaTimeSeconds)
        {
            if (!IsActive || deltaTimeSeconds <= 0f) return;

            AdvanceElapsed(deltaTimeSeconds);
            Run.Tick(deltaTimeSeconds);

            if (Run.State == EAbilityPipelineState.Completed)
            {
                End(ContinuousEndReason.Completed);
                return;
            }

            if (Run.State == EAbilityPipelineState.Failed)
            {
                End(ContinuousEndReason.Interrupted);
            }
        }

        public bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info)
        {
            TryGetContextSource(out var source);
            var sourceContextId = source.SourceContextId != 0 ? source.SourceContextId : SourceContextId;
            info = new MobaContinuousRuntimeDebugInfo(
                _kind,
                ConfigId,
                SourceActorId,
                TargetActorId,
                sourceContextId,
                source.ParentContextId,
                source.RootContextId,
                source.OwnerContextId,
                source.SkillRuntimeHandle,
                source);
            return ConfigId != 0 || SourceActorId > 0 || TargetActorId > 0 || sourceContextId != 0;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
            if (Context is IMobaCombatExecutionContextProvider provider && provider.TryGetCombatExecutionContext(out context))
            {
                return context.HasExecutionSource;
            }

            if (!TryGetContextSource(out var source) || !source.HasExecutionSource)
            {
                return false;
            }

            var combatSource = new MobaCombatContextSource(
                source.ContextKind != EffectContextKind.Unknown ? source.ContextKind : EffectContextKind.ContinuousPeriodic,
                source.TraceKind != MobaTraceKind.None ? source.TraceKind : MobaTraceKind.EffectExecution,
                source.SourceActorId != 0 ? source.SourceActorId : SourceActorId,
                source.TargetActorId != 0 ? source.TargetActorId : TargetActorId,
                source.SourceContextId != 0 ? source.SourceContextId : SourceContextId,
                source.RootContextId != 0 ? source.RootContextId : source.SourceContextId,
                source.OwnerContextId != 0 ? source.OwnerContextId : source.SourceContextId,
                source.ConfigId != 0 ? source.ConfigId : ConfigId,
                source.TriggerId,
                source.Frame,
                source.SkillRuntimeHandle,
                source.RuntimeKind ?? _kind,
                source.RuntimeConfigId != 0 ? source.RuntimeConfigId : ConfigId,
                true);
            context = MobaCombatContextBuilder.FromSource(this, in combatSource);
            return context.HasExecutionSource;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (_source.IsValid)
            {
                source = new MobaContextSourceView(
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.LiveRuntime,
                    _source.ContextKind != EffectContextKind.Unknown ? _source.ContextKind : EffectContextKind.ContinuousPeriodic,
                    _source.TraceKind != MobaTraceKind.None ? _source.TraceKind : MobaTraceKind.EffectExecution,
                    _source.SourceActorId != 0 ? _source.SourceActorId : SourceActorId,
                    _source.TargetActorId != 0 ? _source.TargetActorId : TargetActorId,
                    _source.SourceContextId != 0 ? _source.SourceContextId : SourceContextId,
                    _source.ParentContextId,
                    _source.RootContextId,
                    _source.OwnerContextId,
                    _source.ConfigId != 0 ? _source.ConfigId : ConfigId,
                    _source.TriggerId,
                    _source.Frame,
                    _kind,
                    ConfigId,
                    true,
                    _source.SkillRuntimeHandle);
                return source.IsValid;
            }

            if (Context is IMobaContextSourceProvider provider && provider.TryGetContextSource(out source) && source.IsValid)
            {
                source = new MobaContextSourceView(
                    MobaContextSourceResolveKind.DirectProvider,
                    MobaContextSourceBoundary.LiveRuntime,
                    source.ContextKind != EffectContextKind.Unknown ? source.ContextKind : EffectContextKind.ContinuousPeriodic,
                    source.TraceKind != MobaTraceKind.None ? source.TraceKind : MobaTraceKind.EffectExecution,
                    source.SourceActorId != 0 ? source.SourceActorId : SourceActorId,
                    source.TargetActorId != 0 ? source.TargetActorId : TargetActorId,
                    source.SourceContextId != 0 ? source.SourceContextId : SourceContextId,
                    source.ParentContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    source.ConfigId != 0 ? source.ConfigId : ConfigId,
                    source.TriggerId,
                    source.Frame,
                    _kind,
                    ConfigId,
                    true,
                    source.SkillRuntimeHandle);
                return source.IsValid;
            }

            var origin = new MobaGameplayOrigin(
                SourceActorId,
                TargetActorId,
                MobaTraceKind.EffectExecution,
                ConfigId,
                SourceContextId,
                SourceContextId,
                SourceContextId,
                SourceContextId);
            var lineageContext = origin.ToLineageContext(EffectContextKind.ContinuousPeriodic);
            source = MobaContextSourceView.FromLineage(
                in lineageContext,
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime,
                default,
                true,
                _kind,
                ConfigId);
            return source.IsValid;
        }

        private static int ResolveSourceActorId(IAbilityPipelineContext context)
        {
            return context != null ? context.GetSourceActorId() : 0;
        }

        private static int ResolveTargetActorId(IAbilityPipelineContext context, int fallbackActorId)
        {
            var targetActorId = context != null ? context.GetTargetActorId() : 0;
            return targetActorId > 0 ? targetActorId : fallbackActorId;
        }

        private static long ResolveSourceContextId(IAbilityPipelineContext context)
        {
            return context != null ? context.GetSourceContextId() : 0L;
        }

        private static int CreateModifierSourceId(long sourceContextId, int configId, int ownerActorId, int sourceActorId, int targetActorId)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + configId;
                hash = hash * 31 + ownerActorId;
                hash = hash * 31 + sourceActorId;
                hash = hash * 31 + targetActorId;
                return hash == 0 ? configId : hash;
            }
        }

        private sealed class MobaPipelineContinuousConfig : MobaContinuousConfigBase
        {
            private readonly MobaPipelineContinuousRuntime<TCtx> _runtime;

            public MobaPipelineContinuousConfig(
                MobaPipelineContinuousRuntime<TCtx> runtime,
                ContinuousTagRequirements requirements,
                IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
                : base(0f, requirements, modifiers)
            {
                _runtime = runtime;
            }

            public override string Id => $"pipeline_continuous:{_runtime.OwnerActorId}:{_runtime.SourceActorId}:{_runtime.TargetActorId}:{_runtime.ConfigId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.OwnerActorId;
            public override int OwnerActorId => _runtime.OwnerActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public override GameplayTagSource TagSource => CreateSource(_runtime);

            private static GameplayTagSource CreateSource(MobaPipelineContinuousRuntime<TCtx> runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.OwnerActorId != 0) return new GameplayTagSource(runtime.OwnerActorId);
                return GameplayTagSource.System;
            }
        }
    }
}
