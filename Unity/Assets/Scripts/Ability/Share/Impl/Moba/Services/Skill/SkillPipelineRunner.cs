using System;
using System.Collections.Generic;
using AbilityKit.Ability;

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
            IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase> castPhases,
            object abilityInstance,
            in SkillCastRequest request)
        {
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, out _);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase> castPhases,
            object abilityInstance,
            in SkillCastRequest request,
            SkillCastContext triggerContext)
        {
            return Start(preCastConfig, preCastPhases, castConfig, castPhases, abilityInstance, in request, triggerContext, out _);
        }

        public bool Start(
            IAbilityPipelineConfig preCastConfig,
            IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase> castPhases,
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
            IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
            IAbilityPipelineConfig castConfig,
            IReadOnlyList<IAbilityPipelinePhase> castPhases,
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
                if (ok && entry.Pipeline != null && entry.Pipeline.State == EAbilityPipelineState.Executing)
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

            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastStart, entry.TriggerContext);

            var state = entry.Pipeline.Execute(entry.PreCastConfig, entry.AbilityInstance, entry.Request, entry.TriggerContext);
            if (state == EAbilityPipelineState.Executing) return true;
            if (state == EAbilityPipelineState.Completed)
            {
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastComplete, entry.TriggerContext);
                // Immediately chain to Cast.
                return StartCast(ref entry);
            }

            entry.FailReason = TryGetFailReason(entry.Pipeline);
            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);
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

            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastStart, entry.TriggerContext);

            var state = entry.Pipeline.Execute(entry.CastConfig, entry.AbilityInstance, entry.Request, entry.TriggerContext);
            if (state == EAbilityPipelineState.Executing)
            {
                entry.Stage = EntryStage.Cast;
                return true;
            }

            if (state != EAbilityPipelineState.Completed)
            {
                entry.FailReason = TryGetFailReason(entry.Pipeline);
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);
            }
            else
            {
                MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastComplete, entry.TriggerContext);
            }

            return state == EAbilityPipelineState.Completed;
        }

        private static string TryGetFailReason(IAbilityPipeline pipeline)
        {
            if (pipeline?.Context == null) return null;
            if (pipeline.Context.SharedData == null) return null;
            return pipeline.Context.GetData<string>(MobaSkillPipelineSharedKeys.FailReason, null);
        }

        public void CancelAll()
        {
            if (_running.Count == 0) return;
            for (int i = 0; i < _running.Count; i++)
            {
                var e = _running[i];
                var p = e.Pipeline;

                if (e.Stage == EntryStage.PreCast)
                {
                    MobaSkillTriggering.Publish(e.Request.EventBus, MobaSkillTriggering.Events.PreCastInterrupt, e.TriggerContext);
                }
                else
                {
                    MobaSkillTriggering.Publish(e.Request.EventBus, MobaSkillTriggering.Events.CastInterrupt, e.TriggerContext);
                }

                p?.Interrupt();
            }
            _running.Clear();
        }

        public void Step(float deltaTime)
        {
            if (_running.Count == 0) return;

            for (int i = _running.Count - 1; i >= 0; i--)
            {
                var entry = _running[i];
                var p = entry.Pipeline;
                if (p == null)
                {
                    _running.RemoveAt(i);
                    continue;
                }

                if (p.State == EAbilityPipelineState.Executing)
                {
                    p.OnUpdate(p.Context, deltaTime);
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
                    }

                    if (p.State != EAbilityPipelineState.Completed)
                    {
                        entry.FailReason = entry.FailReason ?? TryGetFailReason(p);
                        LastFailReason = entry.FailReason;

                        if (entry.Stage == EntryStage.PreCast)
                        {
                            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.PreCastFail, entry.TriggerContext, entry.FailReason);
                        }
                        else
                        {
                            MobaSkillTriggering.Publish(entry.Request.EventBus, MobaSkillTriggering.Events.CastFail, entry.TriggerContext, entry.FailReason);
                        }
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
            public IAbilityPipeline Pipeline;
            public string FailReason;

            public readonly IAbilityPipelineConfig PreCastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase> PreCastPhases;
            public readonly IAbilityPipelineConfig CastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase> CastPhases;
            public readonly object AbilityInstance;
            public readonly SkillCastRequest Request;
            public readonly SkillCastContext TriggerContext;

            public Entry(
                IAbilityPipelineConfig preCastConfig,
                IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
                IAbilityPipelineConfig castConfig,
                IReadOnlyList<IAbilityPipelinePhase> castPhases,
                object abilityInstance,
                SkillCastRequest request,
                SkillCastContext triggerContext)
            {
                Stage = EntryStage.PreCast;
                Pipeline = null;
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
