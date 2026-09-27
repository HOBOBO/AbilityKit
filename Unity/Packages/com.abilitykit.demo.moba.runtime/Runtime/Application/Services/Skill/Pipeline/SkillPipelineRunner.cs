using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Core.Logging;
using AbilityKit.Effect;
using AbilityKit.Core.Mathematics;
using AbilityKit.Continuous;
using AbilityKit.Pipeline;
using AbilityKit.Demo.Moba.Services.Search;

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

        public readonly struct SkillPipelineStartResult
        {
            public SkillPipelineStartResult(
                bool success,
                string failReason,
                in SkillPipelineStartReject startReject,
                in SkillPipelineFailure pipelineFailure)
            {
                Success = success;
                FailReason = failReason;
                StartReject = startReject;
                PipelineFailure = pipelineFailure;
            }

            public bool Success { get; }
            public string FailReason { get; }
            public SkillPipelineStartReject StartReject { get; }
            public SkillPipelineFailure PipelineFailure { get; }
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
            return UpdateInputBySlot(slot, in aimPos, in aimDir, targetActorId,
                !aimPos.Equals(Vec3.Zero), !aimDir.Equals(Vec3.Zero), targetActorId > 0);
        }

        public bool UpdateInputBySlot(int slot, in Vec3 aimPos, in Vec3 aimDir, int targetActorId,
            bool hasAimPos, bool hasAimDir, bool hasTarget)
        {
            if (slot <= 0) return false;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var e = _running[i];
                if (e.Context == null) continue;
                if (e.Context.SkillSlot != slot) continue;

                e.Context.UpdateInput(in aimPos, in aimDir, targetActorId, hasAimPos, hasAimDir, hasTarget);
                SynchronizeInput(ref e);

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

        public bool SignalRecastBySlot(int slot)
        {
            if (slot <= 0) return false;
            for (var i = _running.Count - 1; i >= 0; i--)
            {
                var context = _running[i].Context;
                if (context == null || context.SkillSlot != slot || context.WorldServices == null) continue;
                if (!context.TryGetSkillRuntimeHandle(out var handle)) continue;
                if (!context.WorldServices.TryResolve<MobaSkillWindowRuntimeService>(out var windows) || windows == null) continue;
                return windows.TrySignalRecast(in handle);
            }
            return false;
        }

        private static void SynchronizeInput(ref Entry entry)
        {
            var context = entry.Context;
            if (context == null) return;
            var aimPos = context.AimPos;
            var aimDir = context.AimDir;
            entry.Request = new SkillCastRequest(context.SkillId, context.SkillSlot, context.CasterActorId,
                context.TargetActorId, in aimPos, in aimDir, entry.Request.WorldServices,
                entry.Request.EventBus, context.CasterUnit, context.TargetUnit);
            if (entry.TriggerContext != null)
            {
                entry.TriggerContext.AimPos = aimPos;
                entry.TriggerContext.AimDir = aimDir;
                entry.TriggerContext.TargetActorId = context.TargetActorId;
                entry.TriggerContext.TargetUnit = context.TargetUnit;
            }
            if (context.WorldServices != null && context.RuntimeId != 0 &&
                context.WorldServices.TryResolve<MobaSkillCastRuntimeService>(out var runtimes) && runtimes != null)
                runtimes.UpdateInput(context.RuntimeId, in aimPos, in aimDir, context.TargetActorId,
                    true, true, true);
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
            var result = TryStart(
                preCastConfig,
                preCastPhases,
                castConfig,
                castPhases,
                abilityInstance,
                in request,
                triggerContext,
                in policy);
            failReason = result.FailReason;
            return result.Success;
        }

        public SkillPipelineStartResult TryStart(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            SkillCastContext triggerContext,
            in SkillCastPolicy policy)
        {
            LastFailReason = null;
            LastStartReject = SkillPipelineStartReject.None;
            LastPipelineFailure = SkillPipelineFailure.None;

            for (var i = 0; i < _running.Count; i++)
            {
                var running = _running[i];
                if (!Conflicts(in policy, in running.Policy)) continue;
                if (!policy.InterruptRunning || running.Policy.Uninterruptible ||
                    policy.InterruptPriority < running.Policy.InterruptPriority)
                    return RejectStart(in request, triggerContext, SkillFailureCodes.Start.AlreadyRunning, "Skill is already running.");
            }

            if (triggerContext == null)
            {
                return RejectStart(in request, null, SkillFailureCodes.Start.ContextMissing, "Skill cast context is required.");
            }

            if (castConfig == null)
            {
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.CastConfigMissing, "Skill cast pipeline config is missing.");
            }
            if (castPhases == null || castPhases.Count == 0)
            {
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.CastPhasesMissing, "Skill cast pipeline phases are missing.");
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
                triggerContext,
                policy);

            try
            {
                var ft = request.WorldServices != null ? request.WorldServices.Resolve<IFrameTime>() : null;
                if (ft == null)
                {
                    return RejectStart(in request, triggerContext, SkillFailureCodes.Start.FrameTimeMissing, "IFrameTime is required to start skill pipeline.");
                }

                entry.StartFrame = ft.Frame.Value;
            }
            catch (Exception ex)
            {
                const string message = "Failed to resolve skill pipeline start frame.";
                Log.Exception(ex, $"[SkillPipelineRunner] {message} actor={request.CasterActorId} skillId={request.SkillId}");
                return RejectStart(in request, triggerContext, SkillFailureCodes.Start.FrameResolveFailed, message);
            }

            if (policy.TargetLostPolicy != SkillTargetLostPolicy.Legacy && request.TargetActorId > 0)
            {
                entry.Context = new SkillPipelineContext();
                entry.Context.Initialize(abilityInstance, in request, triggerContext);
                if (!ResolveLostTarget(ref entry))
                    return RejectStart(in request, triggerContext, SkillFailureCodes.Cast.TargetMissing,
                        "Skill target is no longer valid.");
            }

            for (var i = _running.Count - 1; i >= 0; i--)
            {
                var running = _running[i];
                if (Conflicts(in policy, in running.Policy))
                    CancelAt(i, in running, "InterruptRunning");
            }

            // 如果没有 PreCast，直接进入 Cast。
            if (preCastConfig == null || preCastPhases == null || preCastPhases.Count == 0)
            {
                var ok = StartCast(ref entry);
                if (ok && entry.Run != null && entry.Run.State == EAbilityPipelineState.Executing)
                {
                    _running.Add(entry);
                }
                LastFailReason = entry.FailReason;
                LastPipelineFailure = entry.PipelineFailure;
                return CreateStartResult(ok);
            }

            var started = StartPreCast(ref entry);
            if (started) _running.Add(entry);
            LastFailReason = entry.FailReason;
            LastPipelineFailure = entry.PipelineFailure;
            return CreateStartResult(started);
        }

        private SkillPipelineStartResult RejectStart(in SkillCastRequest request, SkillCastContext triggerContext, string code, string message)
        {
            LastFailReason = message;
            LastStartReject = new SkillPipelineStartReject(code, message);
            _logger.LogSkillFail(request.CasterActorId, request.SkillId, triggerContext?.SourceContextId ?? 0L, message);
            return CreateStartResult(success: false);
        }

        private SkillPipelineStartResult CreateStartResult(bool success)
        {
            var startReject = LastStartReject;
            var pipelineFailure = LastPipelineFailure;
            return new SkillPipelineStartResult(
                success,
                LastFailReason,
                in startReject,
                in pipelineFailure);
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
            entry.Context.SetCastFlowId(0);
            TryBeginPhaseContext(ref entry);
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
                TryEndPhaseContext(ref entry, MobaExecutionEndReason.Completed);
                RunCleanups(in entry, "precast.complete.immediate");
                // 立即衔接到 Cast。
                return StartCast(ref entry);
            }

            entry.FailReason = TryGetFailReason(entry) ?? $"PreCast state={state}";
            MarkPipelineFailure(ref entry, SkillFailureCodes.Pipeline.PreCastFailed, entry.FailReason);
            _logger.LogSkillFail(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, $"PreCastFailed: {entry.FailReason}");
            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);

            TryEndPhaseContext(ref entry, MobaExecutionEndReason.Cancelled);
            TryEndExecutionContext(entry, MobaExecutionEndReason.Cancelled);
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
            entry.Context.SetCastFlowId(entry.TriggerContext?.CastFlowId ?? 0);
            TryBindPipelineContinuous(ref entry);
            TryBeginPhaseContext(ref entry);
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

                TryEndPhaseContext(ref entry, MobaExecutionEndReason.Cancelled);
                TryEndExecutionContext(entry, MobaExecutionEndReason.Cancelled);
            }
            else
            {
                _logger.LogSkillComplete(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, (int)(entry.Context.ElapsedTime * 1000f));
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                TryEndPhaseContext(ref entry, MobaExecutionEndReason.Completed);
                TryEndExecutionContext(entry, MobaExecutionEndReason.Completed);
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

        private static void TryEndExecutionContext(in Entry entry, MobaExecutionEndReason reason)
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

            var contexts = SafeResolve<MobaExecutionContextRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, rootId={rootId}");
            if (contexts == null) return;

            try
            {
                contexts.End(rootId, (int)reason, ResolveCurrentFrame(in entry, entry.StartFrame));
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] ExecutionContexts.End failed (rootId={rootId}, reason={reason})");
            }
        }

        private static bool Conflicts(in SkillCastPolicy incoming, in SkillCastPolicy running)
        {
            if (incoming.ConflictGroup > 0 && running.ConflictGroup > 0)
                return incoming.ConflictGroup == running.ConflictGroup;
            return !incoming.AllowParallel;
        }

        private static void TryBeginPhaseContext(ref Entry entry)
        {
            TryEndPhaseContext(ref entry, MobaExecutionEndReason.Cancelled);

            var rootId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            if (rootId == 0) return;

            var contexts = SafeResolve<MobaExecutionContextRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, rootId={rootId}, stage={GetStageName(entry.Stage)}");
            if (contexts == null) return;

            try
            {
                entry.PhaseContextId = contexts.Create(new MobaExecutionContextCreateRequest(
                    MobaExecutionKind.SkillPhase,
                    entry.Request.SkillId,
                    entry.Request.CasterActorId,
                    entry.Request.TargetActorId,
                    parentContextId: rootId,
                    frame: ResolveCurrentFrame(in entry, entry.StartFrame),
                    castFlowId: entry.Context?.CastFlowId ?? 0)).ContextId;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] ExecutionContexts.Create failed (rootId={rootId}, actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, stage={GetStageName(entry.Stage)})");
                entry.PhaseContextId = 0L;
            }
        }

        private static void TryEndPhaseContext(ref Entry entry, MobaExecutionEndReason reason)
        {
            var phaseContextId = entry.PhaseContextId;
            if (phaseContextId == 0) return;

            entry.PhaseContextId = 0L;
            var contexts = SafeResolve<MobaExecutionContextRegistry>(in entry, $"actor={entry.Request.CasterActorId}, skill={entry.Request.SkillId}, phaseContextId={phaseContextId}, reason={reason}");
            if (contexts == null) return;

            try
            {
                contexts.End(phaseContextId, (int)reason, ResolveCurrentFrame(in entry, entry.StartFrame));
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] ExecutionContexts.End failed (phaseContextId={phaseContextId}, reason={reason})");
            }
        }

        private static MobaSkillRuntimeEndReason ToRuntimeEndReason(MobaExecutionEndReason reason)
        {
            switch (reason)
            {
                case MobaExecutionEndReason.Completed:
                    return MobaSkillRuntimeEndReason.PipelineCompleted;
                case MobaExecutionEndReason.Failed:
                    return MobaSkillRuntimeEndReason.Failed;
                case MobaExecutionEndReason.Dead:
                    return MobaSkillRuntimeEndReason.OwnerRemoved;
                case MobaExecutionEndReason.Cancelled:
                case MobaExecutionEndReason.Interrupted:
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

                var executionEndReason = runtimeEndReason == MobaSkillRuntimeEndReason.OwnerRemoved
                    ? MobaExecutionEndReason.Dead
                    : MobaExecutionEndReason.Cancelled;
                TryEndPhaseContext(ref e, executionEndReason);
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
            TryEndPhaseContext(ref entry, MobaExecutionEndReason.Cancelled);

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

                if (!ResolveLostTarget(ref entry))
                {
                    CancelAt(i, in entry, "TargetLost");
                    ended++;
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
                        TryEndPhaseContext(ref entry, MobaExecutionEndReason.Completed);
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

                        TryEndPhaseContext(ref entry, MobaExecutionEndReason.Completed);
                        TryEndExecutionContext(entry, MobaExecutionEndReason.Completed);

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

                        TryEndPhaseContext(ref entry, MobaExecutionEndReason.Cancelled);
                        TryEndExecutionContext(entry, MobaExecutionEndReason.Cancelled);

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

        private static bool ResolveLostTarget(ref Entry entry)
        {
            var policy = entry.Policy.TargetLostPolicy;
            var context = entry.Context;
            if (policy == SkillTargetLostPolicy.Legacy || context.TargetActorId <= 0) return true;
            var services = context.WorldServices;
            if (services == null || !services.TryResolve<MobaCombatRulesService>(out var rules) || rules == null)
                return false;
            if (rules.IsAlive(context.TargetActorId)) return true;
            if (policy == SkillTargetLostPolicy.Cancel) return false;

            var targetId = 0;
            if (policy == SkillTargetLostPolicy.Reacquire &&
                services.TryResolve<MobaConfigDatabase>(out var configs) && configs != null &&
                configs.TryGetSkill(context.SkillId, out var skill) && skill != null &&
                services.TryResolve<SearchTargetService>(out var search) && search != null &&
                services.TryResolve<MobaActorLookupService>(out var actors) && actors != null &&
                actors.TryGetActorEntity(context.CasterActorId, out var caster) && caster != null && caster.hasTransform)
            {
                var position = caster.transform.Value.Position;
                var candidates = new List<int>(1);
                if (skill.RequiredTargetQueryId > 0)
                    search.TrySearchActorIds(skill.RequiredTargetQueryId, context.CasterActorId,
                        in position, 0, candidates, context.DiagnosticCommandId);
                else if (skill.Range > 0)
                    search.TrySearchActorIds(NormalAttackTargetQuery.Create(skill.Range), context.CasterActorId,
                        in position, 0, candidates, context.DiagnosticCommandId);
                if (candidates.Count > 0 && rules.IsAlive(candidates[0])) targetId = candidates[0];
            }

            if (policy == SkillTargetLostPolicy.Reacquire && targetId == 0) return false;
            var aimPos = context.AimPos;
            var aimDir = context.AimDir;
            context.UpdateInput(in aimPos, in aimDir, targetId, false, false, true);
            SynchronizeInput(ref entry);
            return true;
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
                        rootContextId: runtimeHandle.RootContextId,
                        sourceContextId: sourceContextId,
                        runtimeHandle: runtimeHandle),
                    MobaBattleExceptionSeverity.Recoverable);
            }
            else if (diagnostics != null)
            {
                var diagnosticContext = new MobaBattleDiagnosticContext(
                    runtimeHandle.RootContextId,
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
            public long PhaseContextId;

            public int StartFrame;

            public readonly IAbilityPipelineConfig PreCastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> PreCastPhases;
            public readonly IAbilityPipelineConfig CastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> CastPhases;
            public readonly object AbilityInstance;
            public SkillCastRequest Request;
            public readonly SkillCastContext TriggerContext;
            public readonly SkillCastPolicy Policy;

            public Entry(
                IAbilityPipelineConfig preCastConfig,
                IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> preCastPhases,
                IAbilityPipelineConfig castConfig,
                IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> castPhases,
                object abilityInstance,
                SkillCastRequest request,
                SkillCastContext triggerContext,
                SkillCastPolicy policy)
            {
                Stage = EntryStage.PreCast;
                Pipeline = null;
                Run = null;
                Context = null;
                FailReason = null;
                PipelineFailure = SkillPipelineFailure.None;
                PhaseContextId = 0L;

                StartFrame = 0;
                PreCastConfig = preCastConfig;
                PreCastPhases = preCastPhases;
                CastConfig = castConfig;
                CastPhases = castPhases;
                AbilityInstance = abilityInstance;
                Request = request;
                TriggerContext = triggerContext;
                Policy = policy;
            }
        }
    }
}
