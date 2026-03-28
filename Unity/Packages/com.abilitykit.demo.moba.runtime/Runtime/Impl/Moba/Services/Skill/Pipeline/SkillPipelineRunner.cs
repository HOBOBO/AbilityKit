using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Impl.Moba.Conponents;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
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

        private readonly int _actorId;
        private readonly List<Entry> _running = new List<Entry>(4);
        private readonly List<RunningSnapshot> _ended = new List<RunningSnapshot>(2);

        public string LastFailReason { get; private set; }

        public SkillPipelineRunner(int actorId)
        {
            _actorId = actorId;
        }

        public int ActorId => _actorId;

        public bool HasRunning => _running.Count > 0;

        public void FillRunningSnapshots(List<RunningSnapshot> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();
            if (_running.Count == 0) return;

            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                if (e.Context == null) continue;

                var instanceId = 0L;
                try { instanceId = e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L; }
                catch { instanceId = 0L; }

                var stage = e.Stage == EntryStage.PreCast ? SkillCastStage.PreCast : SkillCastStage.Cast;

                var elapsedMs = 0;
                try { elapsedMs = (int)(e.Context.ElapsedTime * 1000f); }
                catch { elapsedMs = 0; }

                var nextEventIndex = 0;
                try { nextEventIndex = e.Context.GetData<int>(MobaSkillPipelineSharedKeys.TimelineNextEventIndex); }
                catch { nextEventIndex = 0; }

                buffer.Add(new RunningSnapshot(
                    instanceId: instanceId,
                    ownerActorId: e.Context.CasterActorId,
                    skillId: e.Context.SkillId,
                    skillSlot: e.Context.SkillSlot,
                    skillLevel: e.TriggerContext != null ? e.TriggerContext.SkillLevel : 0,
                    startFrame: e.StartFrame,
                    sequence: e.TriggerContext != null ? e.TriggerContext.Sequence : 0,
                    targetActorId: e.Context.TargetActorId,
                    aimPos: e.Context.AimPos,
                    aimDir: e.Context.AimDir,
                    stage: stage,
                    elapsedMs: elapsedMs,
                    nextEventIndex: nextEventIndex));
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
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, out _);
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
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext: null, out failReason, allowParallel: allowParallel, interruptRunning: interruptRunning);
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
            failReason = null;
            LastFailReason = null;

            if (!allowParallel && _running.Count > 0)
            {
                if (interruptRunning)
                {
                    SkillLogger.Instance.LogSkillCancel(request.CasterActorId, request.SkillId, triggerContext?.SourceContextId ?? 0, "InterruptRunning");
                    CancelAll();
                }
                else
                {
                    failReason = "Skill is already running.";
                    LastFailReason = failReason;
                    SkillLogger.Instance.LogSkillFail(request.CasterActorId, request.SkillId, triggerContext?.SourceContextId ?? 0, failReason);
                    return false;
                }
            }

            if (castConfig == null)
            {
                SkillLogger.Instance.LogWarning($"Start failed: castConfig is null. Caster={request.CasterActorId} SkillId={request.SkillId}");
                return false;
            }
            if (castPhases == null || castPhases.Count == 0)
            {
                SkillLogger.Instance.LogWarning($"Start failed: castPhases is null or empty. Caster={request.CasterActorId} SkillId={request.SkillId}");
                return false;
            }

            triggerContext ??= SkillCastContext.FromRequest(in request, skillLevel: 0);

            SkillLogger.Instance.LogSkillStart(
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
                entry.StartFrame = ft != null ? ft.Frame.Value : 0;
            }
            catch
            {
                entry.StartFrame = 0;
            }

            // If PreCast is missing, go straight to Cast.
            if (preCastConfig == null || preCastPhases == null || preCastPhases.Count == 0)
            {
                var ok = StartCast(ref entry);
                if (ok && entry.Run != null && entry.Run.State == EAbilityPipelineState.Executing)
                {
                    _running.Add(entry);
                }
                failReason = entry.FailReason;
                LastFailReason = entry.FailReason;
                return ok;
            }

            var started = StartPreCast(ref entry);
            if (started) _running.Add(entry);
            failReason = entry.FailReason;
            LastFailReason = entry.FailReason;
            return started;
        }

        private static bool StartPreCast(ref Entry entry)
        {
            entry.Stage = EntryStage.PreCast;
            entry.Pipeline = new SkillCastPipeline();
            for (int i = 0; i < entry.PreCastPhases.Count; i++)
            {
                entry.Pipeline.AddPhase(entry.PreCastPhases[i]);
            }

            entry.Context = new SkillPipelineContext();
            entry.Context.Initialize(entry.AbilityInstance, in entry.Request, entry.TriggerContext);
            entry.Run = entry.Pipeline.Start(entry.PreCastConfig, entry.Context);

            var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            SkillLogger.Instance.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "PreCast", "Starting");

            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastStart, entry.TriggerContext);

            entry.Run.Tick(0f);
            var state = entry.Run.State;
            if (state == EAbilityPipelineState.Executing) return true;
            if (state == EAbilityPipelineState.Completed)
            {
                SkillLogger.Instance.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "PreCast", "Completed");
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                try { entry.Context?.RunAndClearCleanups(); }
                catch { }
                // Immediately chain to Cast.
                return StartCast(ref entry);
            }

            entry.FailReason = TryGetFailReason(entry);
            SkillLogger.Instance.LogSkillFail(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, $"PreCastFailed: {entry.FailReason}");
            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);

            TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);
            try { entry.Context?.RunAndClearCleanups(); }
            catch { }
            return false;
        }

        private static bool StartCast(ref Entry entry)
        {
            entry.Stage = EntryStage.Cast;
            entry.Pipeline = new SkillCastPipeline();
            for (int i = 0; i < entry.CastPhases.Count; i++)
            {
                entry.Pipeline.AddPhase(entry.CastPhases[i]);
            }

            entry.Context = new SkillPipelineContext();
            entry.Context.Initialize(entry.AbilityInstance, in entry.Request, entry.TriggerContext);
            entry.Run = entry.Pipeline.Start(entry.CastConfig, entry.Context);

            var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
            SkillLogger.Instance.LogSkillStage(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, "Cast", "Starting");

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
                entry.FailReason = TryGetFailReason(entry);
                SkillLogger.Instance.LogSkillFail(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, $"CastFailed: {entry.FailReason}");
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);

                TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);
            }
            else
            {
                SkillLogger.Instance.LogSkillComplete(entry.TriggerContext.CasterActorId, entry.TriggerContext.SkillId, instanceId, (int)(entry.Context.ElapsedTime * 1000f));
                MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                TryEndEffectSource(entry, EffectSourceEndReason.Completed);
            }

            try { entry.Context?.RunAndClearCleanups(); }
            catch { }

            return state == EAbilityPipelineState.Completed;
        }

        private static void TryEndEffectSource(in Entry entry, EffectSourceEndReason reason)
        {
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

            EffectSourceRegistry effectSource = null;
            try
            {
                effectSource = entry.Request.WorldServices != null ? entry.Request.WorldServices.Resolve<EffectSourceRegistry>() : null;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[SkillPipelineRunner] resolve EffectSourceRegistry failed");
                effectSource = null;
            }

            if (effectSource == null) return;

            var frame = 0;
            try
            {
                var ft = entry.Request.WorldServices != null ? entry.Request.WorldServices.Resolve<IFrameTime>() : null;
                frame = ft != null ? ft.Frame.Value : 0;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[SkillPipelineRunner] resolve/read IFrameTime failed");
                frame = 0;
            }

            try
            {
                try
                {
                    effectSource.EnsureRoot(
                        contextId: rootId,
                        kind: EffectSourceKind.SkillCast,
                        configId: entry.Request.SkillId,
                        sourceActorId: entry.Request.CasterActorId,
                        targetActorId: entry.Request.TargetActorId,
                        frame: frame,
                        originSource: entry.Request.CasterActorId,
                        originTarget: entry.Request.TargetActorId);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[SkillPipelineRunner] EffectSource.EnsureRoot failed (rootId={rootId}, frame={frame})");
                }

                effectSource.End(rootId, frame, reason);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[SkillPipelineRunner] EffectSource.End failed (rootId={rootId}, frame={frame}, reason={reason})");
            }
        }

        private static string TryGetFailReason(in Entry entry)
        {
            if (entry.Context == null) return null;
            return entry.Context.FailReason;
        }

        public void CancelAll()
        {
            if (_running.Count == 0) return;

            SkillLogger.Instance.LogInfo($"CancelAll: ActorId={_actorId} Count={_running.Count}");

            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                var p = e.Run;

                var instanceId = e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L;
                var stageStr = e.Stage == EntryStage.PreCast ? "PreCast" : "Cast";
                SkillLogger.Instance.LogSkillInterrupt(e.TriggerContext.CasterActorId, e.TriggerContext.SkillId, instanceId, stageStr);

                if (e.Stage == EntryStage.PreCast)
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
                }
                else
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
                }

                p?.Interrupt();

                TryEndEffectSource(e, EffectSourceEndReason.Cancelled);

                try { e.Context?.RunAndClearCleanups(); }
                catch { }

                TryAddEndedSnapshot(in e, SkillCastStage.Cancelled);
            }
            _running.Clear();
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

                var p = e.Run;

                var instanceId = e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L;
                var stageStr = e.Stage == EntryStage.PreCast ? "PreCast" : "Cast";
                SkillLogger.Instance.LogSkillCancel(e.TriggerContext.CasterActorId, skillId, instanceId, $"CancelBySkillId_{stageStr}");

                if (e.Stage == EntryStage.PreCast)
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
                }
                else
                {
                    MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
                }

                p?.Interrupt();
                TryEndEffectSource(e, EffectSourceEndReason.Cancelled);

                try { e.Context?.RunAndClearCleanups(); }
                catch { }
                TryAddEndedSnapshot(in e, SkillCastStage.Cancelled);

                _running.RemoveAt(i);
            }
        }

        public void Step(float deltaTime)
        {
            if (_running.Count == 0) return;

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
                    entry.Context.AdvanceTime(deltaTime);
                    p.Tick(deltaTime);

                    var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;
                    var stageStr = entry.Stage == EntryStage.PreCast ? "PreCast" : "Cast";
                    SkillLogger.Instance.LogSkillTick(
                        entry.Context.CasterActorId,
                        entry.Context.SkillId,
                        instanceId,
                        deltaTime,
                        entry.Context.ElapsedTime,
                        $"{stageStr}_{p.State}");
                }

                if (p.State != EAbilityPipelineState.Executing)
                {
                    var instanceId = entry.TriggerContext != null ? entry.TriggerContext.SourceContextId : 0L;

                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.PreCast)
                    {
                        SkillLogger.Instance.LogSkillStage(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, "PreCast", "Completed_ChainingToCast");
                        MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                        try { entry.Context?.RunAndClearCleanups(); }
                        catch { }
                        // Chain to Cast.
                        if (StartCast(ref entry))
                        {
                            _running[i] = entry;
                            continue;
                        }
                    }

                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.Cast)
                    {
                        SkillLogger.Instance.LogSkillComplete(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, (int)(entry.Context.ElapsedTime * 1000f));
                        MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                        TryEndEffectSource(entry, EffectSourceEndReason.Completed);

                        try { entry.Context?.RunAndClearCleanups(); }
                        catch { }

                        TryAddEndedSnapshot(in entry, SkillCastStage.Completed);
                    }

                    if (p.State != EAbilityPipelineState.Completed)
                    {
                        entry.FailReason = entry.FailReason ?? TryGetFailReason(entry);
                        LastFailReason = entry.FailReason;

                        var stageStr = entry.Stage == EntryStage.PreCast ? "PreCast" : "Cast";
                        SkillLogger.Instance.LogSkillFail(entry.Context.CasterActorId, entry.Context.SkillId, instanceId, $"{stageStr}Failed: {entry.FailReason}");

                        if (entry.Stage == EntryStage.PreCast)
                        {
                            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);
                        }
                        else
                        {
                            MobaSkillTriggering.Publish(MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);
                        }

                        TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);

                        try { entry.Context?.RunAndClearCleanups(); }
                        catch { }

                        var terminal = entry.Context != null && entry.Context.IsAborted ? SkillCastStage.Cancelled : SkillCastStage.Failed;
                        TryAddEndedSnapshot(in entry, terminal);
                    }

                    _running.RemoveAt(i);
                }
            }
        }

        private void TryAddEndedSnapshot(in Entry e, SkillCastStage terminalStage)
        {
            if (e.Context == null) return;

            var instanceId = 0L;
            try { instanceId = e.TriggerContext != null ? e.TriggerContext.SourceContextId : 0L; }
            catch { instanceId = 0L; }
            if (instanceId == 0L) return;

            var elapsedMs = 0;
            try { elapsedMs = (int)(e.Context.ElapsedTime * 1000f); }
            catch { elapsedMs = 0; }

            var nextEventIndex = 0;
            try { nextEventIndex = e.Context.GetData<int>(MobaSkillPipelineSharedKeys.TimelineNextEventIndex); }
            catch { nextEventIndex = 0; }

            _ended.Add(new RunningSnapshot(
                instanceId: instanceId,
                ownerActorId: e.Context.CasterActorId,
                skillId: e.Context.SkillId,
                skillSlot: e.Context.SkillSlot,
                skillLevel: e.TriggerContext != null ? e.TriggerContext.SkillLevel : 0,
                startFrame: e.StartFrame,
                sequence: e.TriggerContext != null ? e.TriggerContext.Sequence : 0,
                targetActorId: e.Context.TargetActorId,
                aimPos: e.Context.AimPos,
                aimDir: e.Context.AimDir,
                stage: terminalStage,
                elapsedMs: elapsedMs,
                nextEventIndex: nextEventIndex));
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
