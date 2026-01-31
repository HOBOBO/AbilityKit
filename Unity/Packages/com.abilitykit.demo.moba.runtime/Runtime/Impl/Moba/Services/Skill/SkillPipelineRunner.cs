using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba;
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
        private readonly int _actorId;
        private readonly List<Entry> _running = new List<Entry>(4);

        public string LastFailReason { get; private set; }

        public SkillPipelineRunner(int actorId)
        {
            _actorId = actorId;
        }

        public int ActorId => _actorId;

        public bool HasRunning => _running.Count > 0;

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
                    CancelAll();
                }
                else
                {
                    failReason = "Skill is already running.";
                    LastFailReason = failReason;
                    return false;
                }
            }

            if (castConfig == null) return false;
            if (castPhases == null || castPhases.Count == 0) return false;

            // PreCast is optional.
            triggerContext ??= SkillCastContext.FromRequest(in request, skillLevel: 0);

            var entry = new Entry(
                preCastConfig,
                preCastPhases,
                castConfig,
                castPhases,
                abilityInstance,
                request,
                triggerContext);

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

            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastStart, entry.TriggerContext);

            entry.Run.Tick(0f);
            var state = entry.Run.State;
            if (state == EAbilityPipelineState.Executing) return true;
            if (state == EAbilityPipelineState.Completed)
            {
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                // Immediately chain to Cast.
                return StartCast(ref entry);
            }

            entry.FailReason = TryGetFailReason(entry);
            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);

            TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);
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

            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastStart, entry.TriggerContext);

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
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);

                TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);
            }
            else
            {
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                TryEndEffectSource(entry, EffectSourceEndReason.Completed);
            }

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
            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                var p = e.Run;

                if (e.Stage == EntryStage.PreCast)
                {
                    MobaSkillTriggering.Publish(e.Request.EventBus, MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
                }
                else
                {
                    MobaSkillTriggering.Publish(e.Request.EventBus, MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
                }

                p?.Interrupt();

                TryEndEffectSource(e, EffectSourceEndReason.Cancelled);
            }
            _running.Clear();
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
                }

                if (p.State != EAbilityPipelineState.Executing)
                {
                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.PreCast)
                    {
                        MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                        // Chain to Cast.
                        if (StartCast(ref entry))
                        {
                            _running[i] = entry;
                            continue;
                        }
                    }

                    if (p.State == EAbilityPipelineState.Completed && entry.Stage == EntryStage.Cast)
                    {
                        MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);

                        TryEndEffectSource(entry, EffectSourceEndReason.Completed);
                    }

                    if (p.State != EAbilityPipelineState.Completed)
                    {
                        entry.FailReason = entry.FailReason ?? TryGetFailReason(entry);
                        LastFailReason = entry.FailReason;

                        if (entry.Stage == EntryStage.PreCast)
                        {
                            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);
                        }
                        else
                        {
                            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);
                        }

                        TryEndEffectSource(entry, EffectSourceEndReason.Cancelled);
                    }

                    _running.RemoveAt(i);
                }
            }
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
