using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public sealed class MobaProjectileLaunchContinuous : MobaContinuousRuntimeBase,
        IMobaTickableContinuous,
        IMobaContinuousIntervalState,
        IMobaContinuousRuntimeDebugSource,
        IMobaContinuousExecutionContextProvider
    {
        private readonly MobaProjectileLaunchConfig _config;
        private readonly IMobaProjectileLaunchExecutor _executor;
        private MobaProjectileLaunchResult _result;
        private bool _started;
        private bool _stopped;

        public MobaProjectileLaunchContinuous(in MobaProjectileLaunchRequest request, IMobaProjectileLaunchExecutor executor)
            : this(in request, executor, null, null)
        {
        }

        public MobaProjectileLaunchContinuous(
            in MobaProjectileLaunchRequest request,
            IMobaProjectileLaunchExecutor executor,
            MobaConfigDatabase configs,
            IMobaContinuousTagTemplateRegistry tagTemplates)
        {
            Request = request;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            var process = ResolveProcess(request.ContinuousProcessId, configs);
            var requirements = ResolveRequirements(process, tagTemplates);
            _config = new MobaProjectileLaunchConfig(this, process, requirements);
            IntervalRemainingSeconds = _config.IntervalSeconds;
        }

        public MobaProjectileLaunchRequest Request { get; }
        public MobaProjectileLaunchResult Result => _result;
        public int CasterActorId => Request.CasterActorId;
        public int LauncherActorId => _result.LauncherActorId;
        public int LauncherId => Request.LauncherId;
        public int ProjectileId => Request.ProjectileId;
        public int ConfigId => Request.ContinuousProcessId > 0 ? Request.ContinuousProcessId : LauncherId;
        public int ModifierSourceId => CreateModifierSourceId(SourceContextId, ConfigId, CasterActorId, LauncherId, ProjectileId);
        public long SourceContextId => Request.SourceContext.SourceContextId;
        public float IntervalRemainingSeconds { get; set; }

        public override IContinuousConfig Config => _config;

        protected override bool OnActivating()
        {
            if (_started) return true;

            _started = true;
            var request = Request;
            var started = _executor.TryStartLaunch(in request, out _result);
            if (!started || !_result.Success)
            {
                AbilityKit.Core.Logging.Log.Warning($"[MobaProjectileLaunchContinuous] activation failed. casterActorId={Request.CasterActorId} launcherId={Request.LauncherId} projectileId={Request.ProjectileId} started={started} error={_result.Error ?? "<none>"}");
            }

            return started && _result.Success;
        }

        public void TickManaged(float deltaTimeSeconds)
        {
            if (!IsActive || deltaTimeSeconds <= 0f) return;

            AdvanceElapsed(deltaTimeSeconds);
            if (_started && _executor.IsLaunchComplete(in _result))
            {
                End(ContinuousEndReason.Completed);
            }
        }

        protected override void OnEnding(ContinuousEndReason reason)
        {
            StopLaunch(reason);
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
                source.TraceKind != MobaTraceKind.None ? source.TraceKind : MobaTraceKind.ProjectileLaunch,
                source.SourceActorId != 0 ? source.SourceActorId : CasterActorId,
                source.TargetActorId,
                source.SourceContextId != 0 ? source.SourceContextId : SourceContextId,
                source.RootContextId != 0 ? source.RootContextId : source.SourceContextId,
                source.OwnerContextId != 0 ? source.OwnerContextId : source.SourceContextId,
                source.ConfigId != 0 ? source.ConfigId : ConfigId,
                source.TriggerId,
                source.Frame,
                source.SkillRuntimeHandle,
                source.RuntimeKind ?? "ProjectileLaunch",
                source.RuntimeConfigId != 0 ? source.RuntimeConfigId : ConfigId,
                true);
            context = MobaCombatContextBuilder.FromSource(this, in combatSource);
            return context.HasExecutionSource;
        }

        public bool TryGetRuntimeDebugInfo(out MobaContinuousRuntimeDebugInfo info)
        {
            TryGetContextSource(out var source);
            var sourceContextId = source.SourceContextId != 0 ? source.SourceContextId : Request.SourceContext.SourceContextId;
            info = new MobaContinuousRuntimeDebugInfo(
                "ProjectileLaunch",
                LauncherId,
                CasterActorId,
                0,
                sourceContextId,
                source.ParentContextId,
                source.RootContextId,
                source.OwnerContextId,
                Request.SourceContext.SkillRuntimeHandle,
                source);
            return CasterActorId > 0 || LauncherId > 0 || ProjectileId > 0 || sourceContextId != 0;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            if (Request.SourceContext.TryGetContextSource(out source))
            {
                return source.IsValid;
            }

            var origin = new MobaGameplayOrigin(
                CasterActorId,
                0,
                MobaTraceKind.ProjectileLaunch,
                ConfigId,
                SourceContextId,
                SourceContextId,
                SourceContextId,
                SourceContextId,
                Request.SourceContext.SkillRuntimeHandle);
            var lineageContext = origin.ToLineageContext(EffectContextKind.ContinuousPeriodic);
            source = MobaContextSourceView.FromLineage(
                in lineageContext,
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime,
                default,
                true,
                "ProjectileLaunch",
                ConfigId);
            return source.IsValid;
        }

        private void StopLaunch(ContinuousEndReason reason)
        {
            if (_stopped) return;
            _stopped = true;

            if (_result.Success)
            {
                _executor.StopLaunch(in _result, reason);
            }
        }

        private static ContinuousProcessMO ResolveProcess(int processId, MobaConfigDatabase configs)
        {
            if (processId <= 0 || configs == null) return null;
            return configs.TryGetContinuousProcess(processId, out var process) ? process : null;
        }

        private static ContinuousTagRequirements ResolveRequirements(ContinuousProcessMO process, IMobaContinuousTagTemplateRegistry tagTemplates)
        {
            var requirements = ResolveTemplateRequirements(process != null ? process.ContinuousTagTemplateId : 0, tagTemplates);
            if (process?.Tags != null && process.Tags.Count > 0)
            {
                requirements.ApplicationTags.AppendTags(process.Tags);
            }

            return requirements;
        }

        private static ContinuousTagRequirements ResolveTemplateRequirements(int templateId, IMobaContinuousTagTemplateRegistry tagTemplates)
        {
            if (templateId <= 0 || tagTemplates == null)
            {
                return new ContinuousTagRequirements();
            }

            if (!tagTemplates.TryGet(templateId, out var template) || template == null)
            {
                return new ContinuousTagRequirements();
            }

            return CopyRequirements(template);
        }

        private static ContinuousTagRequirements CopyRequirements(ContinuousTagRequirements source)
        {
            if (source == null) return new ContinuousTagRequirements();
            return new ContinuousTagRequirements
            {
                ActivationRequired = source.ActivationRequired,
                ApplicationTags = CopyContainer(source.ApplicationTags),
                RemovalRequired = source.RemovalRequired,
                OngoingRequired = source.OngoingRequired,
                RemovalTags = CopyContainer(source.RemovalTags)
            };
        }

        private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
        {
            var copy = new GameplayTagContainer();
            copy.AppendTags(source);
            return copy;
        }

        private static int CreateModifierSourceId(long sourceContextId, int configId, int casterActorId, int launcherId, int projectileId)
        {
            unchecked
            {
                var hash = 23;
                hash = hash * 31 + sourceContextId.GetHashCode();
                hash = hash * 31 + configId;
                hash = hash * 31 + casterActorId;
                hash = hash * 31 + launcherId;
                hash = hash * 31 + projectileId;
                return hash == 0 ? configId : hash;
            }
        }

        private sealed class MobaProjectileLaunchConfig : MobaContinuousConfigBase, IMobaContinuousOwnerBoundTriggerConfig
        {
            private readonly MobaProjectileLaunchContinuous _runtime;
            private readonly ContinuousProcessMO _process;

            public MobaProjectileLaunchConfig(MobaProjectileLaunchContinuous runtime, ContinuousProcessMO process, ContinuousTagRequirements requirements)
                : base(
                    ResolveDurationSeconds(runtime, process),
                    requirements ?? new ContinuousTagRequirements(),
                    process?.Modifiers)
            {
                _runtime = runtime;
                _process = process;
            }

            public override string Id => $"projectile_launch:{_runtime.CasterActorId}:{_runtime.LauncherId}:{_runtime.ProjectileId}:{_runtime.ConfigId}:{_runtime.SourceContextId}";
            public override long OwnerId => _runtime.CasterActorId;
            public override bool CanBeInterrupted => true;
            public override int OwnerActorId => _runtime.CasterActorId;
            public override int ModifierSourceId => _runtime.ModifierSourceId;
            public override GameplayTagSource TagSource => CreateSource(_runtime);
            public override float IntervalSeconds => _process != null && _process.IntervalMs > 0 ? _process.IntervalMs / 1000f : 0f;
            public override IReadOnlyList<int> IntervalEffectIds => _process?.IntervalTriggerIds ?? Array.Empty<int>();
            public IReadOnlyList<int> TriggerIds => _process?.TriggerIds ?? Array.Empty<int>();

            private static float ResolveDurationSeconds(MobaProjectileLaunchContinuous runtime, ContinuousProcessMO process)
            {
                if (process != null && process.DurationMs > 0) return process.DurationMs / 1000f;
                return runtime != null && runtime.Request.DurationMs > 0 ? runtime.Request.DurationMs / 1000f : 0f;
            }

            private static GameplayTagSource CreateSource(MobaProjectileLaunchContinuous runtime)
            {
                if (runtime == null) return GameplayTagSource.System;
                if (runtime.SourceContextId != 0) return new GameplayTagSource(runtime.SourceContextId);
                if (runtime.CasterActorId != 0) return new GameplayTagSource(runtime.CasterActorId);
                return GameplayTagSource.System;
            }
        }
    }
}
