using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaBattleDiagnosticsService
    {
        long GetTimestamp();
        bool IsEnabled(string channel);
        bool ShouldSample(string channel);
        MobaBattleDiagnosticScope Measure(string metricName, double warnThresholdMs = 0d, string context = null);
        void RecordDuration(string metricName, long startTimestamp, double warnThresholdMs = 0d, string context = null);
        void Counter(string counterName, long value = 1L);
        void Gauge(string gaugeName, long value);
        void Sample(string sampleName, double value);
        void Warning(string key, string message, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit);
        void Warning(string key, string message, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit);
        void Warning(string key, Func<string> messageFactory, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit);
        void Warning(string key, Func<string> messageFactory, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit);
        void Exception(string key, Exception exception, string context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit);
        void Exception(string key, Exception exception, string context, in MobaBattleDiagnosticContext diagnosticContext, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit);
        void RecordInputBatchAccepted(int acceptedCount, int handledCount);
        void RecordInputCommandRejected(MobaInputCommandFailureCode failureCode);
        void RecordInputCommandException();
        void RecordSnapshotRouterHealth(in MobaSnapshotRouterHealth health);
        void RecordLifecycleHealth(in MobaTemporaryEntityLifecycleHealth health);
        IReadOnlyList<MobaBattleDiagnosticWarningRecord> GetWarningsSnapshot();
        IReadOnlyList<MobaBattleDiagnosticExceptionRecord> GetExceptionsSnapshot();
        MobaBattleDiagnosticsSnapshot GetSnapshot();
    }

    public readonly struct MobaBattleDiagnosticContext
    {
        public MobaBattleDiagnosticContext(long rootContextId = 0L, long sourceContextId = 0L, MobaSkillCastRuntimeHandle runtimeHandle = default, int actorId = 0, int skillId = 0, string detail = null)
        {
            RootContextId = rootContextId != 0L ? rootContextId : runtimeHandle.RootTraceContextId;
            SourceContextId = sourceContextId;
            RuntimeHandle = runtimeHandle;
            ActorId = actorId;
            SkillId = skillId;
            Detail = detail;
        }

        public long RootContextId { get; }
        public long SourceContextId { get; }
        public MobaSkillCastRuntimeHandle RuntimeHandle { get; }
        public int ActorId { get; }
        public int SkillId { get; }
        public string Detail { get; }
        public bool IsEmpty => RootContextId == 0L && SourceContextId == 0L && !RuntimeHandle.IsValid && ActorId == 0 && SkillId == 0 && string.IsNullOrEmpty(Detail);

        public string FormatSuffix()
        {
            if (IsEmpty) return string.Empty;

            var message = string.Empty;
            if (RootContextId != 0L) message += " rootContextId=" + RootContextId;
            if (SourceContextId != 0L) message += " sourceContextId=" + SourceContextId;
            if (RuntimeHandle.IsValid) message += " runtimeHandle=" + RuntimeHandle + " runtimeRoot=" + RuntimeHandle.RootTraceContextId;
            if (ActorId != 0) message += " actor=" + ActorId;
            if (SkillId != 0) message += " skill=" + SkillId;
            if (!string.IsNullOrEmpty(Detail)) message += " " + Detail;
            return message;
        }
    }

    public readonly struct MobaBattleDiagnosticWarningRecord
    {
        public MobaBattleDiagnosticWarningRecord(string key, string message, int count, bool suppressedAtLimit, in MobaBattleDiagnosticContext context = default)
        {
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
            Count = count;
            SuppressedAtLimit = suppressedAtLimit;
            Context = context;
        }

        public string Key { get; }
        public string Message { get; }
        public int Count { get; }
        public bool SuppressedAtLimit { get; }
        public MobaBattleDiagnosticContext Context { get; }
    }

    public readonly struct MobaBattleDiagnosticExceptionRecord
    {
        public MobaBattleDiagnosticExceptionRecord(string key, string exceptionType, string message, int count, bool suppressedAtLimit, in MobaBattleDiagnosticContext context)
        {
            Key = key ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
            Message = message ?? string.Empty;
            Count = count;
            SuppressedAtLimit = suppressedAtLimit;
            Context = context;
        }

        public string Key { get; }
        public string ExceptionType { get; }
        public string Message { get; }
        public int Count { get; }
        public bool SuppressedAtLimit { get; }
        public MobaBattleDiagnosticContext Context { get; }
    }

    public readonly struct MobaInputDiagnosticAggregate
    {
        public MobaInputDiagnosticAggregate(long acceptedBatches, long acceptedCommands, long handledCommands, long rejectedCommands, long commandExceptions)
        {
            AcceptedBatches = acceptedBatches;
            AcceptedCommands = acceptedCommands;
            HandledCommands = handledCommands;
            RejectedCommands = rejectedCommands;
            CommandExceptions = commandExceptions;
        }

        public long AcceptedBatches { get; }
        public long AcceptedCommands { get; }
        public long HandledCommands { get; }
        public long RejectedCommands { get; }
        public long CommandExceptions { get; }
    }

    public readonly struct MobaSnapshotDiagnosticAggregate
    {
        public MobaSnapshotDiagnosticAggregate(MobaSnapshotRouterHealth health)
        {
            Health = health;
        }

        public MobaSnapshotRouterHealth Health { get; }
    }

    public readonly struct MobaBattleDiagnosticsSnapshot
    {
        public MobaBattleDiagnosticsSnapshot(
            long timestamp,
            ProfilerSnapshot profiler,
            IReadOnlyList<MobaBattleDiagnosticWarningRecord> warnings,
            IReadOnlyList<MobaBattleDiagnosticExceptionRecord> exceptions,
            MobaInputDiagnosticAggregate input,
            MobaSnapshotDiagnosticAggregate snapshot,
            IReadOnlyList<MobaTemporaryEntityLifecycleHealth> lifecycle)
        {
            Timestamp = timestamp;
            Profiler = profiler;
            Warnings = warnings;
            Exceptions = exceptions;
            Input = input;
            Snapshot = snapshot;
            Lifecycle = lifecycle;
        }

        public long Timestamp { get; }
        public ProfilerSnapshot Profiler { get; }
        public IReadOnlyList<MobaBattleDiagnosticWarningRecord> Warnings { get; }
        public IReadOnlyList<MobaBattleDiagnosticExceptionRecord> Exceptions { get; }
        public MobaInputDiagnosticAggregate Input { get; }
        public MobaSnapshotDiagnosticAggregate Snapshot { get; }
        public IReadOnlyList<MobaTemporaryEntityLifecycleHealth> Lifecycle { get; }
        public bool HasProfilerSnapshot => Profiler.Counters != null || Profiler.Gauges != null || Profiler.Samples != null;
    }

    public static class MobaBattleDiagnosticsDefaults
    {
        public const int DefaultWarningLimit = 3;
        public const int DefaultExceptionLimit = 3;
        public const int DefaultHotPathSampleInterval = 4;
        public const int DefaultRunnerSampleInterval = 1;

        public const double ContinuousTickWarnMs = 1.0d;
        public const double BuffDrainWarnMs = 1.0d;
        public const double DamagePipelineWarnMs = 0.5d;
        public const double DamageStageWarnMs = 0.25d;
        public const double EffectsStepWarnMs = 2.0d;
        public const double SkillPipelineStepWarnMs = 2.0d;
        public const double SkillRunnerStepWarnMs = 0.75d;
    }

    public static class MobaBattleDiagnosticChannel
    {
        public const string TriggerHook = "moba.diagnostics.channel.triggerHook";
        public const string PipelineHook = "moba.diagnostics.channel.pipelineHook";
        public const string SkillRunner = "moba.diagnostics.channel.skillRunner";
    }

    public static class MobaBattleDiagnosticMetric
    {
        public const string ContinuousTick = "moba.continuous.tick";
        public const string BuffDrain = "moba.buff.drain";
        public const string DamagePipeline = "moba.damage.pipeline";
        public const string DamageStage = "moba.damage.stage";
        public const string EffectsStep = "moba.effects.step";
        public const string SkillPipelineStep = "moba.skill.pipeline.step";
        public const string SkillRunnerStep = "moba.skill.runner.step";
        public const string TraceRoots = "moba.trace.roots";
        public const string TraceActiveRoots = "moba.trace.active.roots";
        public const string TraceRetainedRoots = "moba.trace.retained.roots";
        public const string TraceRetainedEndedRoots = "moba.trace.retained.ended.roots";
        public const string TraceStaleRetainedRoots = "moba.trace.retained.stale.roots";
        public const string SkillRuntimeActive = "moba.skill.runtime.active";
        public const string SkillRuntimeWaitingChildren = "moba.skill.runtime.waiting.children";
        public const string SkillRuntimePendingChildren = "moba.skill.runtime.pending.children";
        public const string TriggerRegistered = "moba.trigger.registered";
        public const string TriggerUnregistered = "moba.trigger.unregistered";
        public const string TriggerDispatchStarted = "moba.trigger.dispatch.started";
        public const string TriggerDispatchCompleted = "moba.trigger.dispatch.completed";
        public const string TriggerDispatchDuration = "moba.trigger.dispatch.durationMs";
        public const string TriggerDispatchExecuted = "moba.trigger.dispatch.executed";
        public const string TriggerDispatchShortCircuited = "moba.trigger.dispatch.shortCircuited";
        public const string TriggerEvaluated = "moba.trigger.evaluated";
        public const string TriggerEvaluatePassed = "moba.trigger.evaluate.passed";
        public const string TriggerEvaluateFailed = "moba.trigger.evaluate.failed";
        public const string TriggerEvaluateDuration = "moba.trigger.evaluate.durationMs";
        public const string TriggerExecuted = "moba.trigger.executed";
        public const string TriggerExecuteDuration = "moba.trigger.execute.durationMs";
        public const string TriggerShortCircuit = "moba.trigger.shortCircuit";
        public const string EffectActionInvoked = "moba.effect.action.invoked";
        public const string EffectActionSucceeded = "moba.effect.action.succeeded";
        public const string EffectActionFailed = "moba.effect.action.failed";
        public const string EffectActionDuration = "moba.effect.action.durationMs";
        public const string EffectActionAllocatedBytes = "moba.effect.action.allocatedBytes";
        public const string TriggerActionInterrupted = "moba.trigger.action.interrupted";
        public const string TriggerActionFailed = "moba.trigger.action.failed";
        public const string PipelineTraceEvent = "moba.pipeline.trace.event";
        public const string PipelineRunStarted = "moba.pipeline.run.started";
        public const string PipelineRunEnded = "moba.pipeline.run.ended";
        public const string PipelinePhaseStarted = "moba.pipeline.phase.started";
        public const string PipelinePhaseCompleted = "moba.pipeline.phase.completed";
        public const string PipelinePhaseError = "moba.pipeline.phase.error";
        public const string PipelineTick = "moba.pipeline.tick";
        public const string PipelinePaused = "moba.pipeline.paused";
        public const string PipelineResumed = "moba.pipeline.resumed";
        public const string PipelineInterrupted = "moba.pipeline.interrupted";
        public const string SkillRunnerRunning = "moba.skill.runner.running";
        public const string SkillRunnerTicked = "moba.skill.runner.ticked";
        public const string SkillRunnerEnded = "moba.skill.runner.ended";
        public const string SkillRunnerCleanupExceptions = "moba.skill.runner.cleanupExceptions";
        public const string PlanActionSkipped = "moba.planAction.skipped";
        public const string PlanActionRejected = "moba.planAction.rejected";
        public const string PlanActionApplied = "moba.planAction.applied";
        public const string PlanActionTrace = "moba.planAction.trace";
        public const string InputBatchAccepted = "moba.input.batch.accepted";
        public const string InputBatchAcceptedCount = "moba.input.batch.accepted.count";
        public const string InputBatchHandledCount = "moba.input.batch.handled.count";
        public const string InputCommandRejected = "moba.input.command.rejected";
        public const string InputCommandException = "moba.input.command.exception";
        public const string SnapshotRequest = "moba.snapshot.request";
        public const string SnapshotBatchRequest = "moba.snapshot.batch.request";
        public const string SnapshotHit = "moba.snapshot.hit";
        public const string SnapshotEmpty = "moba.snapshot.empty";
        public const string SnapshotEmitterCount = "moba.snapshot.emitters";
        public const string SnapshotBatchSize = "moba.snapshot.batch.size";
        public const string ExceptionPrefix = "moba.exception.";
        public const string TempEntityPrefix = "moba.temp_entity.";
        public const string TempEntityProjectilePrefix = "moba.temp_entity.projectile.";
        public const string TempEntityAreaPrefix = "moba.temp_entity.area.";
        public const string TempEntitySummonPrefix = "moba.temp_entity.summon.";
        public const string TempEntityActiveSuffix = "active";
        public const string TempEntitySpawnedSuffix = "spawned";
        public const string TempEntityDespawnedSuffix = "despawned";
        public const string TempEntityRejectedSuffix = "rejected";
        public const string TempEntityReplacedSuffix = "replaced";
        public const string TempEntityTicksSuffix = "ticks";
        public const string TempEntityHitsSuffix = "hits";
        public const string TempEntityEntersSuffix = "enters";
        public const string TempEntityExitsSuffix = "exits";
        public const string TempEntityExpiresSuffix = "expires";
    }

    public readonly struct MobaBattleDiagnosticScope : IDisposable
    {
        private readonly IMobaBattleDiagnosticsService _diagnostics;
        private readonly string _metricName;
        private readonly string _context;
        private readonly long _startTimestamp;
        private readonly double _warnThresholdMs;

        public MobaBattleDiagnosticScope(IMobaBattleDiagnosticsService diagnostics, string metricName, long startTimestamp, double warnThresholdMs, string context)
        {
            _diagnostics = diagnostics;
            _metricName = metricName;
            _startTimestamp = startTimestamp;
            _warnThresholdMs = warnThresholdMs;
            _context = context;
        }

        public void Dispose()
        {
            _diagnostics?.RecordDuration(_metricName, _startTimestamp, _warnThresholdMs, _context);
        }
    }

    public static class MobaDependencyResolveDiagnostics
    {
        public static void LogSkillExecutionDependencies(IWorldResolver services, string owner)
        {
            if (services == null)
            {
                MobaRuntimeLog.Error(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Validation, owner, "Cannot log skill execution dependency diagnostics because resolver is null.");
                return;
            }

            if (services is IWorldServiceContainer container)
            {
                MobaRuntimeLog.Error(
                    MobaRuntimeLogModule.Input,
                    MobaRuntimeLogPurpose.Validation,
                    owner,
                    $"Registered: SkillCastCoordinator={container.IsRegistered(typeof(SkillCastCoordinator))}, IFrameTime={container.IsRegistered(typeof(IFrameTime))}, IUnitResolver={container.IsRegistered(typeof(AbilityKit.Ability.Share.ECS.IUnitResolver))}, IMobaSkillPipelineLibrary={container.IsRegistered(typeof(IMobaSkillPipelineLibrary))}, IWorldClock={container.IsRegistered(typeof(IWorldClock))}, IEventBus={container.IsRegistered(typeof(AbilityKit.Triggering.Eventing.IEventBus))}");

                LogTryResolveFailure<IWorldClock>(services, owner, "IWorldClock");
                LogTryResolveFailure<IFrameTime>(services, owner, "IFrameTime");
                LogTryResolveFailure<AbilityKit.Triggering.Eventing.IEventBus>(services, owner, "IEventBus");
                LogTryResolveFailure<AbilityKit.Ability.Share.ECS.IUnitResolver>(services, owner, "IUnitResolver");
                LogTryResolveFailure<MobaSkillLoadoutService>(services, owner, nameof(MobaSkillLoadoutService));
                LogTryResolveFailure<MobaActorLookupService>(services, owner, nameof(MobaActorLookupService));
                LogTryResolveFailure<IMobaSkillPipelineLibrary>(services, owner, nameof(IMobaSkillPipelineLibrary));
            }

            LogResolveException<IMobaSkillPipelineLibrary>(services, owner, nameof(IMobaSkillPipelineLibrary));
            LogResolveException<MobaConfigDatabase>(services, owner, nameof(MobaConfigDatabase));
            LogResolveException<MobaEffectExecutionService>(services, owner, nameof(MobaEffectExecutionService));
            LogResolveException<AbilityKit.Triggering.Eventing.IEventBus>(services, owner, "IEventBus");
        }

        private static void LogTryResolveFailure<T>(IWorldResolver services, string owner, string name) where T : class
        {
            if (services.TryResolve(typeof(T), out _) == false)
            {
                MobaRuntimeLog.Error(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Validation, owner, $"Resolve check failed: {name}");
            }
        }

        private static void LogResolveException<T>(IWorldResolver services, string owner, string name) where T : class
        {
            try
            {
                services.Resolve<T>();
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Exception, owner, $"{name} resolve failed.");
            }
        }
    }

    [WorldService(typeof(IMobaBattleDiagnosticsService), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaBattleDiagnosticsService), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticsService : IMobaBattleDiagnosticsService, IService
    {
        private readonly Dictionary<string, int> _warningCounts = new Dictionary<string, int>(64);
        private readonly Dictionary<string, bool> _channelEnabled = new Dictionary<string, bool>(8);
        private readonly Dictionary<string, int> _sampleIntervals = new Dictionary<string, int>(8);
        private readonly Dictionary<string, int> _sampleCounts = new Dictionary<string, int>(8);
        private readonly List<MobaBattleDiagnosticWarningRecord> _warnings = new List<MobaBattleDiagnosticWarningRecord>(32);
        private readonly List<MobaBattleDiagnosticExceptionRecord> _exceptions = new List<MobaBattleDiagnosticExceptionRecord>(32);
        private readonly MobaTemporaryEntityLifecycleHealth[] _lifecycleHealth = new MobaTemporaryEntityLifecycleHealth[3];
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        private long _inputAcceptedBatches;
        private long _inputAcceptedCommands;
        private long _inputHandledCommands;
        private long _inputRejectedCommands;
        private long _inputCommandExceptions;
        private MobaSnapshotRouterHealth _snapshotHealth;

        public long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        public bool IsEnabled(string channel)
        {
            if (string.IsNullOrEmpty(channel)) return true;
            return !_channelEnabled.TryGetValue(channel, out var enabled) || enabled;
        }

        public bool ShouldSample(string channel)
        {
            if (!IsEnabled(channel)) return false;

            var interval = GetSampleInterval(channel);
            if (interval <= 1) return true;

            _sampleCounts.TryGetValue(channel, out var count);
            count++;
            if (count == int.MaxValue) count = 0;
            _sampleCounts[channel] = count;
            return count % interval == 0;
        }

        public void SetChannelEnabled(string channel, bool enabled)
        {
            if (string.IsNullOrEmpty(channel)) return;
            _channelEnabled[channel] = enabled;
        }

        public void SetSampleInterval(string channel, int interval)
        {
            if (string.IsNullOrEmpty(channel)) return;
            _sampleIntervals[channel] = interval < 1 ? 1 : interval;
        }

        public MobaBattleDiagnosticScope Measure(string metricName, double warnThresholdMs = 0d, string context = null)
        {
            if (string.IsNullOrEmpty(metricName)) return default;
            return new MobaBattleDiagnosticScope(this, metricName, GetTimestamp(), warnThresholdMs, context);
        }

        public void RecordDuration(string metricName, long startTimestamp, double warnThresholdMs = 0d, string context = null)
        {
            if (string.IsNullOrEmpty(metricName) || startTimestamp == 0L) return;

            var elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks < 0L) return;

            var elapsedNs = elapsedTicks * 1000000000L / Stopwatch.Frequency;
            var elapsedMs = elapsedNs / 1000000.0d;
            ProfilerHub.Record(metricName, elapsedNs);
            ProfilerHub.Sample(metricName + ".ms", elapsedMs);

            if (warnThresholdMs > 0d && elapsedMs >= warnThresholdMs)
            {
                Warning(
                    "slow:" + metricName,
                    () =>
                    {
                        var suffix = string.IsNullOrEmpty(context) ? string.Empty : " " + context;
                        return $"[MobaDiagnostics] Slow path {metricName} elapsed={elapsedMs:0.###}ms threshold={warnThresholdMs:0.###}ms.{suffix}";
                    },
                    maxCount: MobaBattleDiagnosticsDefaults.DefaultWarningLimit);
            }
        }

        public void Counter(string counterName, long value = 1L)
        {
            if (string.IsNullOrEmpty(counterName) || value == 0L) return;
            if (value == 1L) ProfilerHub.Increment(counterName);
            else ProfilerHub.Add(counterName, value);
        }

        public void Gauge(string gaugeName, long value)
        {
            if (string.IsNullOrEmpty(gaugeName)) return;
            ProfilerHub.SetGauge(gaugeName, value);
        }

        public void Sample(string sampleName, double value)
        {
            if (string.IsNullOrEmpty(sampleName)) return;
            ProfilerHub.Sample(sampleName, value);
        }

        public void Warning(string key, string message, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit)
        {
            Warning(key, message, default, maxCount);
        }

        public void Warning(string key, string message, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!ShouldLog(key, maxCount, out var count, out var suppressedAtLimit)) return;

            var finalMessage = AppendContext(message, in context);
            RecordWarningSnapshot(key, finalMessage, count, suppressedAtLimit, in context);
            CollectWarningEvent(in context, finalMessage);
            AbilityKit.Core.Logging.Log.Warning(finalMessage);
            if (suppressedAtLimit)
            {
                var suppressionMessage = $"[MobaDiagnostics] Further diagnostics suppressed for key={key}.";
                RecordWarningSnapshot(key, suppressionMessage, count, true, in context);
                CollectWarningEvent(in context, suppressionMessage);
                AbilityKit.Core.Logging.Log.Warning(suppressionMessage);
            }
        }

        public void Warning(string key, Func<string> messageFactory, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit)
        {
            Warning(key, messageFactory, default, maxCount);
        }

        public void Warning(string key, Func<string> messageFactory, in MobaBattleDiagnosticContext context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultWarningLimit)
        {
            if (messageFactory == null) return;
            if (!ShouldLog(key, maxCount, out var count, out var suppressedAtLimit)) return;

            var message = messageFactory();
            if (string.IsNullOrEmpty(message)) return;

            var finalMessage = AppendContext(message, in context);
            RecordWarningSnapshot(key, finalMessage, count, suppressedAtLimit, in context);
            CollectWarningEvent(in context, finalMessage);
            AbilityKit.Core.Logging.Log.Warning(finalMessage);
            if (suppressedAtLimit)
            {
                var suppressionMessage = $"[MobaDiagnostics] Further diagnostics suppressed for key={key}.";
                RecordWarningSnapshot(key, suppressionMessage, count, true, in context);
                CollectWarningEvent(in context, suppressionMessage);
                AbilityKit.Core.Logging.Log.Warning(suppressionMessage);
            }
        }

        public void Exception(string key, Exception exception, string context, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit)
        {
            Exception(key, exception, context, default, maxCount);
        }

        public void Exception(string key, Exception exception, string context, in MobaBattleDiagnosticContext diagnosticContext, int maxCount = MobaBattleDiagnosticsDefaults.DefaultExceptionLimit)
        {
            if (exception == null) return;
            if (!ShouldLog(key, maxCount, out var count, out var suppressedAtLimit)) return;

            var message = string.IsNullOrEmpty(context) ? exception.Message : context;
            var finalMessage = AppendContext(message, in diagnosticContext);
            RecordExceptionSnapshot(key, exception.GetType().FullName, finalMessage, count, suppressedAtLimit, in diagnosticContext);
            CollectExceptionEvent(in diagnosticContext, exception, finalMessage);
            AbilityKit.Core.Logging.Log.Exception(exception, $"[MobaDiagnostics] {finalMessage}");
            if (suppressedAtLimit)
            {
                AbilityKit.Core.Logging.Log.Warning($"[MobaDiagnostics] Further exceptions suppressed for key={key}.");
            }
        }

        private void CollectWarningEvent(in MobaBattleDiagnosticContext context, string message)
        {
            var collector = _eventCollector;
            if (collector == null) return;

            try
            {
                var draft = MobaExceptionDiagnosticProducer.CreateWarningDraft(in context, message);
                collector.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                AbilityKit.Core.Logging.Log.Exception(ex, "[MobaDiagnostics] diagnostic collect failed (Warning)");
            }
        }

        private void CollectExceptionEvent(
            in MobaBattleDiagnosticContext context,
            Exception exception,
            string message)
        {
            var collector = _eventCollector;
            if (collector == null) return;

            try
            {
                var draft = MobaExceptionDiagnosticProducer.CreateExceptionDraft(in context, exception, message);
                collector.TryCollect(in draft);
            }
            catch (Exception ex)
            {
                AbilityKit.Core.Logging.Log.Exception(ex, "[MobaDiagnostics] diagnostic collect failed (Exception)");
            }
        }

        public void RecordInputBatchAccepted(int acceptedCount, int handledCount)
        {
            _inputAcceptedBatches++;
            if (acceptedCount > 0) _inputAcceptedCommands += acceptedCount;
            if (handledCount > 0) _inputHandledCommands += handledCount;
        }

        public void RecordInputCommandRejected(MobaInputCommandFailureCode failureCode)
        {
            _inputRejectedCommands++;
        }

        public void RecordInputCommandException()
        {
            _inputCommandExceptions++;
        }

        public void RecordSnapshotRouterHealth(in MobaSnapshotRouterHealth health)
        {
            _snapshotHealth = health;
        }

        public void RecordLifecycleHealth(in MobaTemporaryEntityLifecycleHealth health)
        {
            var index = (int)health.Kind;
            if (index < 0 || index >= _lifecycleHealth.Length) return;
            _lifecycleHealth[index] = health;
        }

        public IReadOnlyList<MobaBattleDiagnosticWarningRecord> GetWarningsSnapshot()
        {
            return _warnings.ToArray();
        }

        public IReadOnlyList<MobaBattleDiagnosticExceptionRecord> GetExceptionsSnapshot()
        {
            return _exceptions.ToArray();
        }

        public MobaBattleDiagnosticsSnapshot GetSnapshot()
        {
            var lifecycle = new MobaTemporaryEntityLifecycleHealth[_lifecycleHealth.Length];
            Array.Copy(_lifecycleHealth, lifecycle, _lifecycleHealth.Length);
            return new MobaBattleDiagnosticsSnapshot(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                GetProfilerSnapshot(),
                _warnings.ToArray(),
                _exceptions.ToArray(),
                new MobaInputDiagnosticAggregate(_inputAcceptedBatches, _inputAcceptedCommands, _inputHandledCommands, _inputRejectedCommands, _inputCommandExceptions),
                new MobaSnapshotDiagnosticAggregate(_snapshotHealth),
                lifecycle);
        }

        public void Dispose()
        {
            _warningCounts.Clear();
            _channelEnabled.Clear();
            _sampleIntervals.Clear();
            _sampleCounts.Clear();
            _warnings.Clear();
            _exceptions.Clear();
            Array.Clear(_lifecycleHealth, 0, _lifecycleHealth.Length);
            _inputAcceptedBatches = 0L;
            _inputAcceptedCommands = 0L;
            _inputHandledCommands = 0L;
            _inputRejectedCommands = 0L;
            _inputCommandExceptions = 0L;
            _snapshotHealth = default;
        }

        private void RecordWarningSnapshot(string key, string message, int count, bool suppressedAtLimit, in MobaBattleDiagnosticContext context)
        {
            _warnings.Add(new MobaBattleDiagnosticWarningRecord(key, message, count, suppressedAtLimit, in context));
        }

        private void RecordExceptionSnapshot(string key, string exceptionType, string message, int count, bool suppressedAtLimit, in MobaBattleDiagnosticContext context)
        {
            _exceptions.Add(new MobaBattleDiagnosticExceptionRecord(key, exceptionType, message, count, suppressedAtLimit, in context));
        }

        private int GetSampleInterval(string channel)
        {
            if (string.IsNullOrEmpty(channel)) return 1;
            if (_sampleIntervals.TryGetValue(channel, out var interval)) return interval;
            if (channel == MobaBattleDiagnosticChannel.SkillRunner) return MobaBattleDiagnosticsDefaults.DefaultRunnerSampleInterval;
            if (channel == MobaBattleDiagnosticChannel.TriggerHook || channel == MobaBattleDiagnosticChannel.PipelineHook) return MobaBattleDiagnosticsDefaults.DefaultHotPathSampleInterval;
            return 1;
        }

        private bool ShouldLog(string key, int maxCount, out int count, out bool suppressedAtLimit)
        {
            count = 0;
            suppressedAtLimit = false;
            if (maxCount <= 0)
            {
                count = 1;
                return true;
            }

            if (string.IsNullOrEmpty(key)) key = "default";

            _warningCounts.TryGetValue(key, out count);
            if (count >= maxCount) return false;

            count++;
            _warningCounts[key] = count;
            suppressedAtLimit = count == maxCount;
            return true;
        }

        private static ProfilerSnapshot GetProfilerSnapshot()
        {
            var profiler = ProfilerHub.GetEditorProfiler();
            return profiler != null ? profiler.GetSnapshot() : default;
        }

        private static string AppendContext(string message, in MobaBattleDiagnosticContext context)
        {
            var suffix = context.FormatSuffix();
            return string.IsNullOrEmpty(suffix) ? message : message + suffix;
        }
    }
}
