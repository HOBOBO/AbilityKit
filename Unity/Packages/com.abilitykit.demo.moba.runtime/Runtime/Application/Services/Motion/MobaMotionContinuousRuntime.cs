using System;
using System.Collections.Generic;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Continuous;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services.Motion
{
    public sealed class MobaMotionContinuousRuntime : MobaContinuousRuntimeBase,
        IMobaTickableContinuous,
        IMobaContinuousIntervalState,
        IMobaContinuousRuntimeStateSync,
        IMobaContinuousRuntimeDebugSource,
        IMobaContinuousExecutionContextProvider
    {
        private readonly MobaMotionContinuousConfig _config;
        private readonly MobaContextSourceView _source;
        private readonly MobaActorRegistry _actors;
        private readonly IMotionSource _motionSource;
        private readonly MobaMotionHitTriggerRuntime _hitTriggerRuntime;
        private readonly MobaMotionLandingTriggerRuntime _landingTriggerRuntime;
        private readonly MobaMotionLandingTriggerService _landingTriggerService;
        private bool _sourceAdded;
        private bool _sourceStopped;
        private bool _landingTriggered;

        public MobaMotionContinuousRuntime(
            string kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int ownerActorId,
            long sourceContextId,
            MobaActorRegistry actors,
            IMotionSource motionSource,
            float durationSeconds,
            ContinuousTagRequirements requirements,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers,
            IReadOnlyList<int> triggerIds,
            float intervalSeconds,
            IReadOnlyList<int> intervalTriggerIds,
            MobaContextSourceView source = default,
            MobaMotionHitTriggerRuntime hitTriggerRuntime = default,
            MobaMotionLandingTriggerRuntime landingTriggerRuntime = default,
            MobaMotionLandingTriggerService landingTriggerService = null)
        {
            Kind = string.IsNullOrEmpty(kind) ? "MotionContinuous" : kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId > 0 ? targetActorId : sourceActorId;
            OwnerActorId = ownerActorId > 0 ? ownerActorId : TargetActorId;
            SourceContextId = sourceContextId;
            ModifierSourceId = CreateModifierSourceId(sourceContextId, configId, OwnerActorId, SourceActorId, TargetActorId);
            _actors = actors;
            _motionSource = motionSource;
            _source = source;
            _hitTriggerRuntime = hitTriggerRuntime;
            _landingTriggerRuntime = landingTriggerRuntime;
            _landingTriggerService = landingTriggerService;
            _config = new MobaMotionContinuousConfig(
                this,
                durationSeconds,
                requirements ?? new ContinuousTagRequirements(),
                modifiers,
                triggerIds,
                intervalSeconds,
                intervalTriggerIds);
            IntervalRemainingSeconds = _config.IntervalSeconds;
        }

        public string Kind { get; }
        public int ConfigId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int OwnerActorId { get; }
        public long SourceContextId { get; }
        public int ModifierSourceId { get; }
        public override IContinuousConfig Config => _config;

        protected override bool OnActivating()
        {
            if (_actors == null || _motionSource == null || OwnerActorId <= 0)
            {
                Log.Warning($"[MobaMotionContinuousRuntime] activate rejected. kind={Kind}, owner={OwnerActorId}, hasActors={_actors != null}, hasSource={_motionSource != null}");
                return false;
            }

            if (!_actors.TryGet(OwnerActorId, out var entity) || entity == null || !entity.hasMotion)
            {
                Log.Warning($"[MobaMotionContinuousRuntime] activate rejected. kind={Kind}, owner={OwnerActorId}, actorFound={entity != null}, hasMotion={entity != null && entity.hasMotion}");
                return false;
            }

            var motion = entity.motion;
            if (!motion.Initialized || motion.Pipeline == null)
            {
                Log.Warning($"[MobaMotionContinuousRuntime] activate rejected. kind={Kind}, owner={OwnerActorId}, initialized={motion.Initialized}, hasPipeline={motion.Pipeline != null}");
                return false;
            }

            motion.Pipeline.AddSource(_motionSource);
            _sourceAdded = true;
            Log.Info($"[MobaMotionContinuousRuntime] source added. kind={Kind}, owner={OwnerActorId}, sourceActive={_motionSource.IsActive}");

            if (_hitTriggerRuntime.IsValid)
            {
                entity.ReplaceMotion(
                    motion.Pipeline,
                    motion.State,
                    motion.Output,
                    motion.Solver,
                    motion.Policy,
                    motion.Events,
                    motion.Initialized,
                    _hitTriggerRuntime);
            }

            return true;
        }

        protected override void OnPaused()
        {
            End(ContinuousEndReason.Interrupted);
        }

        protected override void OnEnding(ContinuousEndReason reason)
        {
            StopMotionSource();
        }

        public void TickManaged(float deltaTimeSeconds)
        {
            if (!IsActive || deltaTimeSeconds <= 0f) return;

            AdvanceElapsed(deltaTimeSeconds);

            if (_motionSource == null || !_motionSource.IsActive)
            {
                TryExecuteLandingTriggers();
                End(ContinuousEndReason.Completed);
            }
        }

        public void SyncManagedState()
        {
            if (!IsActive) return;
            if (_motionSource == null || !_motionSource.IsActive)
            {
                TryExecuteLandingTriggers();
                End(ContinuousEndReason.Completed);
            }
        }

        public bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info)
        {
            TryGetContextSource(out var source);
            var sourceContextId = source.SourceContextId != 0 ? source.SourceContextId : SourceContextId;
            info = new MobaContinuousRuntimeDebugInfo(
                Kind,
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
                source.RuntimeKind ?? Kind,
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
                    Kind,
                    ConfigId,
                    true,
                    _source.SkillRuntimeHandle);
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
                Kind,
                ConfigId);
            return source.IsValid;
        }

        private void TryExecuteLandingTriggers()
        {
            if (_landingTriggered || !_landingTriggerRuntime.IsValid)
            {
                return;
            }

            _landingTriggered = true;
            if (_actors == null || !_actors.TryGet(OwnerActorId, out var entity) || entity == null)
            {
                return;
            }

            if (entity.hasTransform)
            {
                Log.Info($"[MobaMotionContinuousRuntime] landing completed. kind={Kind}, owner={OwnerActorId}, position={entity.transform.Value.Position}, triggers={_landingTriggerRuntime.TriggerIds.Count}");
            }

            if (_landingTriggerService == null)
            {
                Log.Warning($"[MobaMotionContinuousRuntime] landing trigger skipped. kind={Kind}, owner={OwnerActorId}, reason=missing service");
                return;
            }

            _landingTriggerService.TryExecute(in _landingTriggerRuntime);
        }

        private void StopMotionSource()
        {
            if (_sourceStopped) return;
            _sourceStopped = true;

            _motionSource?.Cancel();

            if (!_sourceAdded || _actors == null || OwnerActorId <= 0)
            {
                return;
            }

            if (!_actors.TryGet(OwnerActorId, out var entity) || entity == null || !entity.hasMotion)
            {
                return;
            }

            var motion = entity.motion;
            motion.Pipeline?.RemoveSource(_motionSource);
        }

        private static int CreateModifierSourceId(long sourceContextId, int configId, int ownerActorId, int sourceActorId, int targetActorId)
        {
            unchecked
            {
                var hash = 19;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + configId;
                hash = hash * 31 + ownerActorId;
                hash = hash * 31 + sourceActorId;
                hash = hash * 31 + targetActorId;
                return hash == 0 ? configId : hash;
            }
        }

        private sealed class MobaMotionContinuousConfig : MobaContinuousConfigBase, IMobaContinuousOwnerBoundTriggerConfig
        {
            private readonly MobaMotionContinuousRuntime _runtime;
            private readonly IReadOnlyList<int> _triggerIds;
            private readonly float _intervalSeconds;
            private readonly IReadOnlyList<int> _intervalTriggerIds;

            public MobaMotionContinuousConfig(
                MobaMotionContinuousRuntime runtime,
                float durationSeconds,
                ContinuousTagRequirements requirements,
                IReadOnlyList<IMobaContinuousModifierSpec> modifiers,
                IReadOnlyList<int> triggerIds,
                float intervalSeconds,
                IReadOnlyList<int> intervalTriggerIds)
                : base(durationSeconds, requirements, modifiers)
            {
                _runtime = runtime;
                _triggerIds = triggerIds ?? Array.Empty<int>();
                _intervalSeconds = intervalSeconds;
                _intervalTriggerIds = intervalTriggerIds ?? Array.Empty<int>();
            }

            public override string Id => $"motion_continuous:{_runtime.Kind}:{_runtime.OwnerActorId}:{_runtime.SourceActorId}:{_runtime.TargetActorId}:{_runtime.ConfigId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.OwnerActorId;
            public override int OwnerActorId => _runtime.OwnerActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public override GameplayTagSource TagSource => CreateSource(_runtime);
            public override float IntervalSeconds => _intervalSeconds;
            public override IReadOnlyList<int> IntervalEffectIds => _intervalTriggerIds;
            public IReadOnlyList<int> TriggerIds => _triggerIds;

            private static GameplayTagSource CreateSource(MobaMotionContinuousRuntime runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.OwnerActorId != 0) return new GameplayTagSource(runtime.OwnerActorId);
                return GameplayTagSource.System;
            }
        }
    }
}
