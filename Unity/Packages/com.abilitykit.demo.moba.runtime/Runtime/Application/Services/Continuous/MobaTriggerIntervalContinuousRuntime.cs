using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaTriggerIntervalContinuousRuntime : MobaContinuousRuntimeBase, IMobaTickableContinuous, IMobaContinuousIntervalState, IMobaContinuousRuntimeDebugSource, IMobaContinuousExecutionContextProvider
    {
        private readonly MobaTriggerIntervalContinuousConfig _config;
        private readonly MobaContextSourceView _source;

        public MobaTriggerIntervalContinuousRuntime(ContinuousProcessMO process, int sourceActorId, int targetActorId, long sourceContextId, ContinuousTagRequirements requirements, MobaContextSourceView source = default)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId != 0 ? targetActorId : sourceActorId;
            SourceContextId = sourceContextId;
            ModifierSourceId = CreateModifierSourceId(sourceContextId, Process.Id, SourceActorId, TargetActorId);
            _source = source;
            _config = new MobaTriggerIntervalContinuousConfig(this, BuildRequirements(requirements, process.Tags), process);
            IntervalRemainingSeconds = _config.IntervalSeconds;
        }

        public ContinuousProcessMO Process { get; }
        public int ProcessId => Process.Id;
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long SourceContextId { get; }
        public int ModifierSourceId { get; }
        public override IContinuousConfig Config => _config;

        public float RemainingSeconds
        {
            get
            {
                var duration = _config.DurationSeconds;
                if (!duration.HasValue) return float.PositiveInfinity;
                var remaining = duration.Value - ElapsedSeconds;
                return remaining > 0f ? remaining : 0f;
            }
        }

        protected override bool OnActivating()
        {
            return ProcessId > 0 && SourceActorId > 0 && SourceContextId != 0;
        }

        public void TickManaged(float deltaTimeSeconds)
        {
            if (!IsActive || deltaTimeSeconds <= 0f) return;

            AdvanceElapsed(deltaTimeSeconds);
            var duration = _config.DurationSeconds;
            if (duration.HasValue && ElapsedSeconds >= duration.Value)
            {
                End(ContinuousEndReason.Completed);
            }
        }

        public bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info)
        {
            TryGetContextSource(out var source);
            var sourceContextId = source.SourceContextId != 0 ? source.SourceContextId : SourceContextId;
            info = new MobaContinuousRuntimeDebugInfo(
                "TriggerIntervalContinuous",
                ProcessId,
                SourceActorId,
                TargetActorId,
                sourceContextId,
                source.ParentContextId,
                source.RootContextId,
                source.OwnerContextId,
                source.SkillRuntimeHandle,
                source);
            return ProcessId > 0 || SourceActorId > 0 || TargetActorId > 0 || sourceContextId != 0;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
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
                source.ConfigId != 0 ? source.ConfigId : ProcessId,
                source.TriggerId,
                source.Frame,
                source.SkillRuntimeHandle,
                source.RuntimeKind ?? "TriggerIntervalContinuous",
                source.RuntimeConfigId != 0 ? source.RuntimeConfigId : ProcessId,
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
                    _source.ConfigId != 0 ? _source.ConfigId : ProcessId,
                    _source.TriggerId,
                    _source.Frame,
                    "TriggerIntervalContinuous",
                    ProcessId,
                    true,
                    _source.SkillRuntimeHandle);
                return source.IsValid;
            }

            var origin = new MobaGameplayOrigin(
                SourceActorId,
                TargetActorId,
                MobaTraceKind.EffectExecution,
                ProcessId,
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
                "TriggerIntervalContinuous",
                ProcessId);
            return source.IsValid;
        }

        private static ContinuousTagRequirements BuildRequirements(ContinuousTagRequirements template, GameplayTagContainer extraTags)
        {
            var result = new ContinuousTagRequirements
            {
                ActivationRequired = template?.ActivationRequired ?? new GameplayTagRequirements(),
                ApplicationTags = CopyContainer(template?.ApplicationTags),
                RemovalRequired = template?.RemovalRequired ?? new GameplayTagRequirements(),
                OngoingRequired = template?.OngoingRequired ?? new GameplayTagRequirements(),
                RemovalTags = CopyContainer(template?.RemovalTags)
            };

            result.ApplicationTags.AppendTags(extraTags);
            return result;
        }

        private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
        {
            var copy = new GameplayTagContainer();
            copy.AppendTags(source);
            return copy;
        }

        private static int CreateModifierSourceId(long sourceContextId, int processId, int sourceActorId, int targetActorId)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + processId;
                hash = hash * 31 + sourceActorId;
                hash = hash * 31 + targetActorId;
                return hash == 0 ? processId : hash;
            }
        }

        private sealed class MobaTriggerIntervalContinuousConfig : MobaContinuousConfigBase, IMobaContinuousOwnerBoundTriggerConfig, IMobaContinuousPeriodicGateConfig
        {
            private readonly MobaTriggerIntervalContinuousRuntime _runtime;
            private readonly ContinuousProcessMO _process;

            public MobaTriggerIntervalContinuousConfig(MobaTriggerIntervalContinuousRuntime runtime, ContinuousTagRequirements requirements, ContinuousProcessMO process)
                : base(process != null && process.DurationMs > 0 ? process.DurationMs / 1000f : 0f, requirements, process?.Modifiers)
            {
                _runtime = runtime;
                _process = process;
            }

            public override string Id => $"trigger_interval_continuous:{_runtime.SourceActorId}:{_runtime.TargetActorId}:{_runtime.ProcessId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.TargetActorId;
            public override int OwnerActorId => _runtime.TargetActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public override GameplayTagSource TagSource => CreateSource(_runtime);
            public override float IntervalSeconds => _process != null && _process.IntervalMs > 0 ? _process.IntervalMs / 1000f : 0f;
            public override IReadOnlyList<int> IntervalEffectIds => _process?.IntervalTriggerIds ?? Array.Empty<int>();
            public IReadOnlyList<int> TriggerIds => _process?.TriggerIds ?? Array.Empty<int>();
            public bool RequireOutOfCombat => _process != null && _process.RequireOutOfCombat;
            public float OutOfCombatSeconds => _process?.OutOfCombatSeconds ?? 0f;

            private static GameplayTagSource CreateSource(MobaTriggerIntervalContinuousRuntime runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.SourceActorId != 0) return new GameplayTagSource(runtime.SourceActorId);
                return GameplayTagSource.System;
            }
        }
    }
}

