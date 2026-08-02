using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Core.Logging;
using AbilityKit.Effect;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Core.Continuous;
using AbilityKit.Pipeline;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillPipelineRunner
    {
        public readonly struct RunningSnapshot
        {
            public readonly long InstanceId;
            public readonly int OwnerActorId;
            public readonly int SkillId;
            public readonly int SkillSlot;
            public readonly int SkillLevel;
            public readonly int StartFrame;
            public readonly int Sequence;
            public readonly int TargetActorId;
            public readonly Vec3 AimPos;
            public readonly Vec3 AimDir;
            public readonly SkillCastStage Stage;
            public readonly int ElapsedMs;
            public readonly int NextEventIndex;

            public RunningSnapshot(
                long instanceId,
                int ownerActorId,
                int skillId,
                int skillSlot,
                int skillLevel,
                int startFrame,
                int sequence,
                int targetActorId,
                Vec3 aimPos,
                Vec3 aimDir,
                SkillCastStage stage,
                int elapsedMs,
                int nextEventIndex)
            {
                InstanceId = instanceId;
                OwnerActorId = ownerActorId;
                SkillId = skillId;
                SkillSlot = skillSlot;
                SkillLevel = skillLevel;
                StartFrame = startFrame;
                Sequence = sequence;
                TargetActorId = targetActorId;
                AimPos = aimPos;
                AimDir = aimDir;
                Stage = stage;
                ElapsedMs = elapsedMs;
                NextEventIndex = nextEventIndex;
            }
        }

        public readonly struct SkillPipelineStartReject
        {
            public static readonly SkillPipelineStartReject None = new SkillPipelineStartReject(null, null);

            public SkillPipelineStartReject(string code, string message)
            {
                Code = code;
                Message = message;
            }

            public string Code { get; }
            public string Message { get; }
            public bool HasValue => !string.IsNullOrEmpty(Code) || !string.IsNullOrEmpty(Message);

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Code)) return string.IsNullOrEmpty(Message) ? "unknown" : Message;
                if (string.IsNullOrEmpty(Message)) return Code;
                return Code + ": " + Message;
            }
        }

        public readonly struct SkillPipelineFailure
        {
            public static readonly SkillPipelineFailure None = new SkillPipelineFailure(null, null, null);

            public SkillPipelineFailure(string stage, string code, string message)
            {
                Stage = stage;
                Code = code;
                Message = message;
            }

            public string Stage { get; }
            public string Code { get; }
            public string Message { get; }
            public bool HasValue => !string.IsNullOrEmpty(Stage) || !string.IsNullOrEmpty(Code) || !string.IsNullOrEmpty(Message);

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Code)) return string.IsNullOrEmpty(Message) ? "unknown" : Message;
                if (string.IsNullOrEmpty(Message)) return string.IsNullOrEmpty(Stage) ? Code : Stage + ": " + Code;
                return string.IsNullOrEmpty(Stage) ? Code + ": " + Message : Stage + ": " + Code + ": " + Message;
            }
        }

        private readonly int _actorId;
        private readonly IMobaBattleDiagnosticsService _diagnostics;
        private readonly IMobaBattleExceptionPolicy _exceptions;
        private readonly ISkillLogger _logger;
        private readonly PipelineRuntime _pipelineRuntime;
        private readonly List<Entry> _running = new List<Entry>(4);
        private readonly List<RunningSnapshot> _ended = new List<RunningSnapshot>(2);

        public string LastFailReason { get; private set; }
        public SkillPipelineStartReject LastStartReject { get; private set; } = SkillPipelineStartReject.None;
        public SkillPipelineFailure LastPipelineFailure { get; private set; } = SkillPipelineFailure.None;

        public SkillPipelineRunner(int actorId, IMobaBattleDiagnosticsService diagnostics = null, IMobaBattleExceptionPolicy exceptions = null, ISkillLogger logger = null)
        {
            _actorId = actorId;
            _diagnostics = diagnostics;
            _exceptions = exceptions;
            _logger = logger ?? SkillLogger.Instance;
            _pipelineRuntime = diagnostics != null
                ? new PipelineRuntime(traceRecorder: new MobaPipelineDiagnosticsRecorder(diagnostics))
                : null;
        }

        public int ActorId => _actorId;

        public bool HasRunning => _running.Count > 0;

        public bool TryGetLatestRunningBySlot(int slot, out RunningSnapshot snapshot)
        {
            snapshot = default;
            if (slot <= 0) return false;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillSlot != slot) continue;

                snapshot = CreateSnapshot(in e, ToSkillCastStage(e.Stage));
                return true;
            }

            return false;
        }

        public bool TryGetRunningByInstanceId(long instanceId, out RunningSnapshot snapshot)
        {
            snapshot = default;
            if (instanceId == 0L) return false;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (GetInstanceId(in e) != instanceId) continue;

                snapshot = CreateSnapshot(in e, ToSkillCastStage(e.Stage));
                return true;
            }

            return false;
        }

        public bool UpdateInputBySlot(int slot, in Vec3 aimPos, in Vec3 aimDir, int targetActorId)
        {
            if (slot <= 0) return false;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillSlot != slot) continue;

                e.Context.UpdateInput(in aimPos, in aimDir, targetActorId);
                if (e.TriggerContext != null)
                {
                    e.TriggerContext.AimPos = e.Context.AimPos;
                    e.TriggerContext.AimDir = e.Context.AimDir;
                    e.TriggerContext.TargetActorId = e.Context.TargetActorId;
                }

                _running[i] = e;
                return true;
            }

            return false;
        }

        public bool MarkReleaseBySlot(int slot)
        {
            if (slot <= 0) return false;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillSlot != slot) continue;

                e.Context.MarkInputReleased();
                _running[i] = e;
                return true;
            }

            return false;
        }

        public void FillRunningSnapshots(List<RunningSnapshot> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();
            if (_running.Count == 0) return;

            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                if (e.Context == null) continue;

                buffer.Add(CreateSnapshot(in e, ToSkillCastStage(e.Stage)));
            }
        }

        public void FillEndedSnapshots(List<RunningSnapshot> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();
            if (_ended.Count == 0) return;

            buffer.AddRange(_ended);
            _ended.Clear();
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request)
        {
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext: null, out _);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            SkillCastContext triggerContext)
        {
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext, out _);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            out string failReason,
            bool allowParallel = false,
            bool interruptRunning = false)
        {
            var policy = new SkillCastPolicy(allowParallel, interruptRunning);
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext: null, out failReason, policy: in policy);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            SkillCastContext triggerContext,
            out string failReason,
            bool allowParallel = false,
            bool interruptRunning = false)
        {
            var policy = new SkillCastPolicy(allowParallel, interruptRunning);
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext, out failReason, policy: in policy);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            SkillCastContext triggerContext,
            out string failReason,
            in SkillCastPolicy policy)
        {
            failReason = null;
            LastFailReason = null;
            LastStartReject = SkillPipelineStartReject.None;
            LastPipelineFailure = SkillPipelineFailure.None;

            if (!policy.AllowParallel && _running.Count > 0)
            {
                if (policy.InterruptRunning)
                {
                    _logger.LogSkillCancel(request.CasterActorId, request.SkillId, triggerContext?.SourceContextId ?? 0, "InterruptRunning");
                    CancelAll();
                }
                else
                {
                    return RejectStart(in request, triggerContext, SkillFailureCodes.Start.AlreadyRunning, "Skill is already running.", out failReason);
                }
            }

            if (triggerContext == null)
            {
                return RejectStart(in request, null, SkillFailureCodes.Start.ContextMissing, "Skill cast context is required.", out failReason);
            }

            if (castConfig == null)
            {
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.CastConfigMissing, "Skill cast pipeline config is missing.", out failReason);
            }
            if (castPhases == null || castPhases.Count == 0)
            {
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.CastPhasesMissing, "Skill cast pipeline phases are missing.", out failReason);
            }

            _logger.LogSkillStart(
                request.CasterActorId,
                request.SkillId,
                request.SkillSlot,
                triggerContext.SkillLevel,
                request.TargetActorId,
                request.AimPos,
                request.AimDir,
                triggerContext.SourceContextId);

            var entry = new Entry(
                preCastConfig,
                preCastPhases,
                castConfig,
                castPhases,
                abilityInstance,
                request,
                triggerContext);

            try
            {
                var ft = request.WorldServices != null ? request.WorldServices.Resolve<IFrameTime>() : null;
                if (ft == null)
                {
                    return RejectStart(in request, triggerContext, SkillFailureCodes.Start.FrameTimeMissing, "IFrameTime is required to start skill pipeline.", out failReason);
                }

                entry.StartFrame = ft.Frame.Value;
            }
            catch (Exception ex)
            {
                const string message = "Failed to resolve skill pipeline start frame.";
                Log.Exception(ex, $"[SkillPipelineRunner] {message} actor={request.CasterActorId} skillId={request.SkillId}");
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.FrameResolveFailed, message, out failReason);
            }

            // 如果没有 PreCast，直接进入 Cast。
            if (preCastConfig == null || preCastPhases == null || preCastPhases.Count == 0)
            {
                var ok = StartCast(ref entry);
                if (ok && entry.Run != null && entry.Run.State == EAbilityPipelineState.Executing)
                {
                    _running.Add(entry);
                }
                failReason = entry.FailReason;
                LastFailReason = entry.FailReason;
                LastPipelineFailure = entry.PipelineFailure;
                return ok;
            }

            var started = StartPreCast(ref entry);
            if (started) _running.Add(entry);
            failReason = entry.FailReason;
            LastFailReason = entry.FailReason;
            LastPipelineFailure = entry.PipelineFailure;
            return started;
        }

        private bool RejectStart(in SkillCastRequest request, SkillCastContext triggerContext, string code, string message, out string failReason)
        {
            failReason = message;
            LastFailReason = message;
            LastStartReject = new SkillPipelineStartReject(code, message);
            _logger.LogSkillFail(request.CasterActorId, request.SkillId, triggerContext?.SourceContextId ?? 0L, message);
            return false;
        }

        private bool StartPreCast(ref Entry entry)
        {
            entry.Stage = EntryStage.PreCast;
            entry.Pipeline = CreatePipeline();
            for (int i = 0; i < entry.PreCastPhases.Count; i++)
            {
                entry.Pipeline.AddPhase(entry.PreCastPhases[i]);
            }

            entry.Context = new SkillPipelineContext();
            entry.Context.Initialize(entry.AbilityInstance, in entry.Request, entry.TriggerContext);
            entry.Context.SetFrame(entry.StartFrame);
            entry.Context.SetPipelineTraceLocation(0, 0L);
            TryBeginPhaseTrace(ref entry);
            entry.Run = entry.Pipeline.Start(entry.PreCastConfig, entry.Context);

            var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            _logger.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "PreCast", "Starting");

            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastStart, entry.TriggerContext);

            entry.Run.Tick(0f);
            var state = entry.Run.State;
            if (state == EAbilityPipelineState.Executing) return true;
            if (state == EAbilityPipelineState.Completed)
            {
                _logger.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "PreCast", "Completed");
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                TryEndPhaseTrace(ref entry, TraceLifecycleReason.Completed);
                RunCleanups(in entry, "precast.complete.immediate");
                // 立即衔接到 Cast。
                return StartCast(ref entry);
            }

            entry.FailReason = TryGetFailReason(entry) ?? $"PreCast state={state}";
            MarkPipelineFailure(ref entry, SkillFailureCodes.Pipeline.PreCastFailed, entry.FailReason);
            _logger.LogSkillFail(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, $"PreCastFailed: {entry.FailReason}");
            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);

            TryEndPhaseTrace(ref entry, TraceLifecycleReason.Cancelled);
            TryEndTraceContext(entry, TraceLifecycleReason.Cancelled);
            RunCleanups(in entry, "precast.failed.immediate");
            return false;
        }

        private bool StartCast(ref Entry entry)
        {
            entry.Stage = EntryStage.Cast;
            entry.Pipeline = CreatePipeline();
            for (int i = 0; i < entry.CastPhases.Count; i++)
            {
                entry.Pipeline.AddPhase(entry.CastPhases[i]);
            }

            entry.Context = new SkillPipelineContext();
            entry.Context.Initialize(entry.AbilityInstance, in entry.Request, entry.TriggerContext);
            entry.Context.SetFrame(ResolveCurrentFrame(in entry, entry.StartFrame));
            entry.Context.SetPipelineTraceLocation(entry.TriggerContext?.CastFlowId ?? 0, 0L);
            TryBindPipelineContinuous(ref entry);
            TryBeginPhaseTrace(ref entry);
            entry.Run = entry.Pipeline.Start(entry.CastConfig, entry.Context);

            var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            _logger.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "Cast", "Starting");

            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastStart, entry.TriggerContext);

            entry.Run.Tick(0f);
            var state = entry.Run.State;
            if (state == EAbilityPipelineState.Executing)
            {
                entry.Stage = EntryStage.Cast;
                return true;
            }

            if (state != EAbilityPipelineState.Completed)
            {
                entry.FailReason = TryGetFailReason(entry) ?? $"Cast state={state}";
                MarkPipelineFailure(ref entry, SkillFailureCodes.Pipeline.CastFailed, entry.FailReason);
                _logger.LogSkillFail(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, $"CastFailed: {entry.FailReason}");
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);

                TryEndPhaseTrace(ref entry, TraceLifecycleReason.Cancelled);
                TryEndTraceContext(entry, TraceLifecycleReason.Cancelled);
            }
            else
            {
                _logger.LogSkillComplete(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, (int)(entry.Context.ElapsedTime * 1000f));
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                TryEndPhaseTrace(ref entry, TraceLifecycleReason.Completed);
                TryEndTraceContext(entry, TraceLifecycleReason.Completed);
            }

            RunCleanups(in entry, state == EAbilityPipelineState.Completed ? "cast.complete.immediate" : "cast.failed.immediate");

            return state == EAbilityPipelineState.Completed;
        }

        private static void TryBindPipelineContinuous(ref Entry entry)
        {
            var context = entry.Context;
            if (context == null) return;
            if (!(entry.CastConfig is MobaSkillPipelineConfig config) || !config.HasPipelineContinuousTagTemplate) return;

            var failContext = $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, config={config.ConfigId}, tagTemplate={config.PipelineContinuousTagTemplateId}";
            var registry = SafeResolve<IMobaContinuousTagTemplateRegistry>(in entry, failContext);
            if (registry == null) return;

            if (!registry.TryGet(config.PipelineContinuousTagTemplateId, out var requirements) || requirements == null)
            {
                Log.Warning($"[SkillPipelineRunner] pipeline continuous tag template missing. {failContext}");
                return;
            }

            var continuous = SafeResolve<IContinuousManager>(in entry, failContext);
            if (continuous == null) return;

            var runtime = new SkillPipelineContinuousRuntime(context, config, requirements);
            if (!continuous.TryActivate(runtime))
            {
                Log.Warning($"[SkillPipelineRunner] pipeline continuous activation failed. actor={context.CasterActorId} skillId={context.SkillId} configId={config.ConfigId} tagTemplateId={config.PipelineContinuousTagTemplateId}");
                return;
            }

            context.RegisterCleanup(() => continuous.TryEnd(runtime, ContinuousEndReason.Completed));
        }

        private static int ReadNextEventIndex(SkillPipelineContext context)
        {
            if (context == null) return -1;
            try
            {
                return context.TimelineNextEventIndex;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] read TimelineNextEventIndex failed (actor={context.CasterActorId}, skill={context.SkillId}, runtime={context.RuntimeId})");
                return -1;
            }
        }

        // 统一的 WorldServices 安全解析入口：空容器返回 null，解析异常被隔离并落日志，避免在运行时各处重复 try/catch。
        private static T SafeResolve<T>(in Entry entry, string failContext) where T : class
        {
            var services = entry.Request.WorldServices;
            if (services == null) return null;

            try
            {
                return services.Resolve<T>();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] resolve {typeof(T).Name} failed ({failContext})");
                return null;
            }
        }

        private static void TryEndSkillRuntime(in Entry entry, MobaSkillRuntimeEndReason reason)
        {
            var handle = entry.TriggerContext != null ? entry.TriggerContext.RuntimeHandle : default;
            if (!handle.IsValid) return;

            var runtimes = SafeResolve<MobaSkillCastRuntimeService>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, handle={handle.RuntimeId}:{handle.Generation}, reason={reason}");
            if (runtimes == null) return;
            runtimes.MarkPipelineEnded(in handle, reason);
        }

        private static void TryCancelSkillRuntime(in Entry entry, MobaSkillRuntimeEndReason reason = MobaSkillRuntimeEndReason.Cancelled)
        {
            var handle = entry.TriggerContext != null ? entry.TriggerContext.RuntimeHandle : default;
            if (!handle.IsValid) return;

            var runtimes = SafeResolve<MobaSkillCastRuntimeService>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, handle={handle.RuntimeId}:{handle.Generation}, reason={reason}");
            if (runtimes == null) return;
            runtimes.Cancel(in handle, reason);
        }

        private static void TryEndTraceContext(in Entry entry, TraceLifecycleReason reason)
        {
            if (entry.TriggerContext != null && entry.TriggerContext.RuntimeHandle.IsValid)
            {
                TryEndSkillRuntime(in entry, ToRuntimeEndReason(reason));
                return;
            }

            var rootId = 0L;
            try
            {
                rootId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[SkillPipelineRunner] read TriggerContext.SourceContextId failed");
                rootId = 0L;
            }

            if (rootId == 0) return;

            var trace = SafeResolve<MobaTraceRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, rootId={rootId}");
            if (trace == null) return;

            try
            {
                try
                {
                    var origin = new TraceOrigin(
                        kind: (int)MobaTraceKind.SkillCast,
                        sourceActorId: entry.Request.CasterActorId,
                        targetActorId: entry.Request.TargetActorId,
                        originSource: TraceEndpoint.Actor(entry.Request.CasterActorId),
                        originTarget: TraceEndpoint.Actor(entry.Request.TargetActorId),
                        configId: entry.Request.SkillId);
                    trace.EnsureRoot(rootId, in origin);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[SkillPipelineRunner] Trace.EnsureRoot failed (rootId={rootId})");
                }

                trace.EndContext(rootId, reason);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] Trace.EndContext failed (rootId={rootId}, reason={reason})");
            }
        }

        private static void TryBeginPhaseTrace(ref Entry entry)
        {
            TryEndPhaseTrace(ref entry, TraceLifecycleReason.Cancelled);

            var rootId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            if (rootId == 0) return;

            var trace = SafeResolve<MobaTraceRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, rootId={rootId}, stage={GetStageName(entry.Stage)}");
            if (trace == null) return;

            try
            {
                entry.PhaseTraceContextId = trace.CreateChildContext(
                    rootId,
                    MobaTraceKind.SkillPhase,
                    entry.Request.SkillId,
                    entry.Request.CasterActorId,
                    entry.Request.TargetActorId,
                    TraceEndpoint.Config(MobaRuntimeKindNames.SkillPipeline, entry.Request.SkillId),
                    TraceEndpoint.Actor(entry.Request.TargetActorId));
                trace.TrySetSkillPhaseLocation(
                    entry.PhaseTraceContextId,
                    entry.Request.SkillId,
                    entry.Context?.CastFlowId ?? 0,
                    string.Empty);
                entry.Context?.SetPipelineTraceLocation(
                    entry.Context.CastFlowId,
                    entry.PhaseTraceContextId);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] Trace.CreateChildContext failed (rootId={rootId}, actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, stage={GetStageName(entry.Stage)})");
                entry.PhaseTraceContextId = 0L;
            }
        }

        private static void TryEndPhaseTrace(ref Entry entry, TraceLifecycleReason reason)
        {
            var phaseContextId = entry.PhaseTraceContextId;
            if (phaseContextId == 0) return;

            entry.PhaseTraceContextId = 0L;
            entry.Context?.SetPipelineTraceLocation(entry.Context.CastFlowId, 0L);
            var trace = SafeResolve<MobaTraceRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, phaseContextId={phaseContextId}, reason={reason}");
            if (trace == null) return;

            try
            {
                trace.EndContext(phaseContextId, reason);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] Trace.EndContext failed (phaseContextId={phaseContextId}, reason={reason})");
            }
        }

        private static MobaSkillRuntimeEndReason ToRuntimeEndReason(TraceLifecycleReason reason)
        {
            switch (reason)
            {
                case TraceLifecycleReason.Completed:
                    return MobaSkillRuntimeEndReason.PipelineCompleted;
                case TraceLifecycleReason.Failed:
                    return MobaSkillRuntimeEndReason.Failed;
                case TraceLifecycleReason.Dead:
                    return MobaSkillRuntimeEndReason.OwnerRemoved;
                case TraceLifecycleReason.Cancelled:
                case TraceLifecycleReason.Interrupted:
                    return MobaSkillRuntimeEndReason.Cancelled;
                default:
                    return MobaSkillRuntimeEndReason.Cancelled;
            }
        }

        private static string TryGetFailReason(in Entry entry)
        {
            if (entry.Context == null) return null;
            return entry.Context.FailReason;
        }

        private static void MarkPipelineFailure(ref Entry entry, string code, string message)
        {
            entry.PipelineFailure = new SkillPipelineFailure(GetStageName(entry.Stage), code, message);
        }

        private static string GetStageName(EntryStage stage)
        {
            return stage == EntryStage.PreCast ? "PreCast" : "Cast";
        }

        private static int ResolveCurrentFrame(in Entry entry, int defaultFrame)
        {
            var time = SafeResolve<IFrameTime>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, defaultFrame={defaultFrame}");
            return time != null ? time.Frame.Value : defaultFrame;
        }

        public void CancelAll()
        {
            CancelAll(MobaSkillRuntimeEndReason.Cancelled);
        }

        public void CancelAll(MobaSkillRuntimeEndReason runtimeEndReason)
        {
            if (_running.Count == 0) return;

            _logger.LogInfo($"CancelAll: ActorId={_actorId} Count={_running.Count} Reason={runtimeEndReason}");

            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                var p = e.Run;

                var instanceId = e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L;
                var stageStr = GetStageName(e.Stage);
                _logger.LogSkillInterrupt(e.TriggerContext.CasterActorId, e.TriggerContext.SkillId, instanceId, stageStr);

                if (e.Stage == EntryStage.PreCast)
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
                }
                else
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
                }

                p?.Interrupt();

                var traceReason = runtimeEndReason == MobaSkillRuntimeEndReason.OwnerRemoved
                    ? TraceLifecycleReason.Dead
                    : TraceLifecycleReason.Cancelled;
                TryEndPhaseTrace(ref e, traceReason);
                TryCancelSkillRuntime(in e, runtimeEndReason);

                RunCleanups(e.Context, "cancelAll");

                TryAddEndedSnapshot(in e, SkillCastStage.Cancelled);
            }
            _running.Clear();
        }

        public bool CancelBySlot(int slot)
        {
            if (slot <= 0) return false;
            if (_running.Count == 0) return false;

            var cancelled = false;
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillSlot != slot) continue;

                CancelAt(i, in e, "CancelBySlot");
                cancelled = true;
            }

            return cancelled;
        }

        public void CancelBySkillId(int skillId)
        {
            if (skillId <= 0) return;
            if (_running.Count == 0) return;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillId != skillId) continue;

                CancelAt(i, in e, "CancelBySkillId");
            }
        }

        private void CancelAt(int index, in Entry e, string reason)
        {
            var p = e.Run;

            var instanceId = GetInstanceId(in e);
            var stageStr = GetStageName(e.Stage);
            _logger.LogSkillCancel(e.Context.CasterActorId, e.Context.SkillId, instanceId, $"{reason}_{stageStr}");

            if (e.Stage == EntryStage.PreCast)
            {
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
            }
            else
            {
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
            }

            p?.Interrupt();
            TryCancelSkillRuntime(in e, MobaSkillRuntimeEndReason.Cancelled);
            var entry = e;
            TryEndPhaseTrace(ref entry, TraceLifecycleReason.Cancelled);
            TryEndTraceContext(entry, TraceLifecycleReason.Cancelled);

            RunCleanups(e.Context, reason);
            TryAddEndedSnapshot(in e, SkillCastStage.Cancelled);

            _running.RemoveAt(index);
        }

        public void Step(float deltaTime)
        {
            if (_running.Count == 0) return;

            var diagnostics = _diagnostics;
            var start = diagnostics != null ? diagnostics.GetTimestamp() : 0L;
            var runningAtStart = _running.Count;
            var ticked = 0;
            var ended = 0;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var entry = _running[i];
                var p = entry.Run;
                if (p == null || entry.Context == null)
                {
                    _running.RemoveAt(i);
                    continue;
                }

                if (p.State == EAbilityPipelineState.Executing)
                {
                    ticked++;
                    entry.Context.SetFrame(ResolveCurrentFrame(in entry, entry.StartFrame));
                    entry.Context.AdvanceTime(deltaTime);
                    p.Tick(deltaTime);
                }

                if (p.State != EAbilityPipelineState.Executing)
                {
                    var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;

                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.PreCast)
                    {
                        _logger.LogSkillStage(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, "PreCast", "Completed_ChainingToCast");
                        MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                        TryEndPhaseTrace(ref entry, TraceLifecycleReason.Completed);
                        RunCleanups(entry.Context, "precast.complete");
                        // 衔接到 Cast。
                        if (StartCast(ref entry) && entry.Run != null && entry.Run.State == EAbilityPipelineState.Executing)
                        {
                            _running[i] = entry;
                            continue;
                        }

                        p = entry.Run;
                        if (entry.PipelineFailure.HasValue)
                        {
                            LastFailReason = entry.FailReason;
                            LastPipelineFailure = entry.PipelineFailure;
                            TryAddEndedSnapshot(in entry, entry.Context != null && entry.Context.IsAborted ? SkillCastStage.Cancelled : SkillCastStage.Failed);
                            _running.RemoveAt(i);
                            ended++;
                            continue;
                        }

                        if (p != null && p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.Cast)
                        {
                            TryAddEndedSnapshot(in entry, SkillCastStage.Completed);
                            _running.RemoveAt(i);
                            ended++;
                            continue;
                        }
                    }

                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.Cast)
                    {
                        _logger.LogSkillComplete(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, (int)(entry.Context.ElapsedTime * 1000f));
                        MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                        TryEndPhaseTrace(ref entry, TraceLifecycleReason.Completed);
                        TryEndTraceContext(entry, TraceLifecycleReason.Completed);

                        RunCleanups(entry.Context, "cast.complete");

                        TryAddEndedSnapshot(in entry, SkillCastStage.Completed);
                    }

                    if (p.State != EAbilityPipelineState.Completed)
                    {
                        entry.FailReason = entry.FailReason ?? TryGetFailReason(entry) ?? $"{GetStageName(entry.Stage)} state={p.State}";
                        MarkPipelineFailure(ref entry, entry.Stage == EntryStage.PreCast ? SkillFailureCodes.Pipeline.PreCastFailed : SkillFailureCodes.Pipeline.CastFailed, entry.FailReason);
                        LastFailReason = entry.FailReason;
                        LastPipelineFailure = entry.PipelineFailure;

                        var stageStr = GetStageName(entry.Stage);
                        _logger.LogSkillFail(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, $"{stageStr}Failed: {entry.FailReason}");

                        if (entry.Stage == EntryStage.PreCast)
                        {
                            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);
                        }
                        else
                        {
                            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);
                        }

                        TryEndPhaseTrace(ref entry, TraceLifecycleReason.Cancelled);
                        TryEndTraceContext(entry, TraceLifecycleReason.Cancelled);

                        RunCleanups(entry.Context, "pipeline.failed");

                        var terminal = entry.Context != null && entry.Context.IsAborted ? SkillCastStage.Cancelled : SkillCastStage.Failed;
                        TryAddEndedSnapshot(in entry, terminal);
                    }

                    _running.RemoveAt(i);
                    ended++;
                }
            }

            if (diagnostics != null && diagnostics.IsEnabled(MobaBattleDiagnosticChannel.SkillRunner))
            {
                if (diagnostics.ShouldSample(MobaBattleDiagnosticChannel.SkillRunner))
                {
                    diagnostics.Sample(MobaBattleDiagnosticMetric.SkillRunnerRunning, runningAtStart);
                    diagnostics.Sample(MobaBattleDiagnosticMetric.SkillRunnerTicked, ticked);
                    diagnostics.Sample(MobaBattleDiagnosticMetric.SkillRunnerEnded, ended);
                }

                diagnostics.RecordDuration(
                    MobaBattleDiagnosticMetric.SkillRunnerStep,
                    start,
                    MobaBattleDiagnosticsDefaults.SkillRunnerStepWarnMs);
            }
        }

        private void RunCleanups(SkillPipelineContext context, string reason)
        {
            try
            {
                context?.RunAndClearCleanups();
            }
            catch (Exception ex)
            {
                var runtimeHandle = context != null ? context.RuntimeHandle : default;
                ReportCleanupException(_exceptions, _diagnostics, ex, _actorId, context != null ? context.SkillId : 0, context != null ? context.RuntimeId : 0L, context != null ? context.SourceContextId : 0L, runtimeHandle, reason);
            }
        }

        private static void RunCleanups(in Entry entry, string reason)
        {
            try
            {
                entry.Context?.RunAndClearCleanups();
            }
            catch (Exception ex)
            {
                var exceptions = ResolveExceptions(in entry);
                var diagnostics = exceptions == null ? ResolveDiagnostics(in entry) : null;
                var actorId = entry.Context != null ? entry.Context.CasterActorId : entry.Request.CasterActorId;
                var skillId = entry.Context != null ? entry.Context.SkillId : entry.Request.SkillId;
                var runtimeId = entry.Context != null ? entry.Context.RuntimeId : 0L;
                var sourceContextId = entry.Context != null ? entry.Context.SourceContextId : entry.TriggerContext?.SourceContextId ?? 0L;
                var runtimeHandle = entry.Context != null ? entry.Context.RuntimeHandle : entry.TriggerContext != null ? entry.TriggerContext.RuntimeHandle : default;
                ReportCleanupException(exceptions, diagnostics, ex, actorId, skillId, runtimeId, sourceContextId, runtimeHandle, reason);
            }
        }

        private static IMobaBattleExceptionPolicy ResolveExceptions(in Entry entry)
        {
            return SafeResolve<IMobaBattleExceptionPolicy>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}");
        }

        private static IMobaBattleDiagnosticsService ResolveDiagnostics(in Entry entry)
        {
            return SafeResolve<IMobaBattleDiagnosticsService>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}");
        }

        private static void ReportCleanupException(IMobaBattleExceptionPolicy exceptions, IMobaBattleDiagnosticsService diagnostics, Exception ex, int actorId, int skillId, long runtimeId, long sourceContextId, MobaSkillCastRuntimeHandle runtimeHandle, string reason)
        {
            if (exceptions != null)
            {
                exceptions.Handle(
                    ex,
                    new MobaBattleExceptionContext(
                        MobaBattleExceptionDomain.Cleanup,
                        "skill.runner.cleanup",
                        actorId: actorId,
                        skillId: skillId,
                        runtimeId: runtimeId,
                        detail: "reason=" + reason,
                        rootContextId: runtimeHandle.RootTraceContextId,
                        sourceContextId: sourceContextId,
                        runtimeHandle: runtimeHandle),
                    MobaBattleExceptionSeverity.Recoverable);
            }
            else if (diagnostics != null)
            {
                var diagnosticContext = new MobaBattleDiagnosticContext(
                    runtimeHandle.RootTraceContextId,
                    sourceContextId,
                    runtimeHandle,
                    actorId,
                    skillId,
                    "reason=" + reason);
                diagnostics.Exception(
                    "skill.runner.cleanup",
                    ex,
                    $"Skill cleanup failed. actor={actorId} reason={reason}",
                    in diagnosticContext);
                diagnostics.Counter(MobaBattleDiagnosticMetric.SkillRunnerCleanupExceptions);
            }
            else
            {
                Log.Exception(ex, $"[SkillPipelineRunner] Skill cleanup failed. actor={actorId} reason={reason}");
            }
        }

        private void TryAddEndedSnapshot(in Entry e, SkillCastStage terminalStage)
        {
            if (e.Context == null) return;
            if (GetInstanceId(in e) == 0L) return;

            _ended.Add(CreateSnapshot(in e, terminalStage));
        }

        private SkillCastPipeline CreatePipeline()
        {
            var pipeline = new SkillCastPipeline();
            if (_pipelineRuntime != null)
            {
                pipeline.Runtime = _pipelineRuntime;
            }

            return pipeline;
        }

        private static SkillCastStage ToSkillCastStage(EntryStage stage)
        {
            return stage == EntryStage.PreCast ? SkillCastStage.PreCast : SkillCastStage.Cast;
        }

        private static long GetInstanceId(in Entry e)
        {
            return e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L;
        }

        private static RunningSnapshot CreateSnapshot(in Entry e, SkillCastStage stage)
        {
            var elapsedMs = e.Context != null ? (int)(e.Context.ElapsedTime * 1000f) : 0;
            var nextEventIndex = e.Context != null ? e.Context.TimelineNextEventIndex : 0;

            return new RunningSnapshot(
                instanceId: GetInstanceId(in e),
                ownerActorId: e.Context != null ? e.Context.CasterActorId : 0,
                skillId: e.Context != null ? e.Context.SkillId : 0,
                skillSlot: e.Context != null ? e.Context.SkillSlot : 0,
                skillLevel: e.TriggerContext != null ? e.TriggerContext.SkillLevel : 0,
                startFrame: e.StartFrame,
                sequence: e.TriggerContext != null ? e.TriggerContext.Sequence : 0,
                targetActorId: e.Context != null ? e.Context.TargetActorId : 0,
                aimPos: e.Context != null ? e.Context.AimPos : Vec3.Zero,
                aimDir: e.Context != null ? e.Context.AimDir : Vec3.Forward,
                stage: stage,
                elapsedMs: elapsedMs,
                nextEventIndex: nextEventIndex);
        }

        private enum EntryStage
        {
            PreCast = 0,
            Cast = 1,
        }

        private struct Entry
        {
            public EntryStage Stage;
            public SkillCastPipeline Pipeline;
            public IAbilityPipelineRun<SkillPipelineContext> Run;
            public SkillPipelineContext Context;
            public string FailReason;
            public SkillPipelineFailure PipelineFailure;
            public long PhaseTraceContextId;

            public int StartFrame;

            public readonly IAbilityPipelineConfig PreCastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> PreCastPhases;
            public readonly IAbilityPipelineConfig CastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> CastPhases;
            public readonly object AbilityInstance;
            public readonly SkillCastRequest Request;
            public readonly SkillCastContext TriggerContext;

            public Entry(
                IAbilityPipelineConfig preCastConfig,
                IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
                IAbilityPipelineConfig castConfig,
                IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
                object abilityInstance,
                SkillCastRequest request,
                SkillCastContext triggerContext)
            {
                Stage = EntryStage.PreCast;
                Pipeline = null;
                Run = null;
                Context = null;
                FailReason = null;
                PipelineFailure = SkillPipelineFailure.None;
                PhaseTraceContextId = 0L;

                StartFrame = 0;
                PreCastConfig = preCastConfig;
                PreCastPhases = preCastPhases;
                CastConfig = castConfig;
                CastPhases = castPhases;
                AbilityInstance = abilityInstance;
                Request = request;
                TriggerContext = triggerContext;
            }
        }
    }
}
