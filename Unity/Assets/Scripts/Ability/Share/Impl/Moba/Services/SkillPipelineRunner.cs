using System;
using System.Collections.Generic;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillPipelineRunner
    {
        private readonly int _actorId;
        private readonly List<Entry> _running = new List<Entry>(4);

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
            if (castConfig == null) return false;
            if (castPhases == null || castPhases.Count == 0) return false;

            // PreCast is optional.
            var entry = new Entry(
                preCastConfig,
                preCastPhases,
                castConfig,
                castPhases,
                abilityInstance,
                request);

            // If PreCast is missing, go straight to Cast.
            if (preCastConfig == null || preCastPhases == null || preCastPhases.Count == 0)
            {
                return StartCast(ref entry);
            }

            var started = StartPreCast(ref entry);
            if (started) _running.Add(entry);
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

            var state = entry.Pipeline.Execute(entry.PreCastConfig, entry.AbilityInstance, entry.Request);
            if (state == EAbilityPipelineState.Executing) return true;
            if (state == EAbilityPipelineState.Completed)
            {
                // Immediately chain to Cast.
                return StartCast(ref entry);
            }

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

            var state = entry.Pipeline.Execute(entry.CastConfig, entry.AbilityInstance, entry.Request);
            if (state == EAbilityPipelineState.Executing)
            {
                entry.Stage = EntryStage.Cast;
                return true;
            }

            return state == EAbilityPipelineState.Completed;
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
                        // Chain to Cast.
                        if (StartCast(ref entry))
                        {
                            _running[i] = entry;
                            continue;
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

            public readonly IAbilityPipelineConfig PreCastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase> PreCastPhases;
            public readonly IAbilityPipelineConfig CastConfig;
            public readonly IReadOnlyList<IAbilityPipelinePhase> CastPhases;
            public readonly object AbilityInstance;
            public readonly SkillCastRequest Request;

            public Entry(
                IAbilityPipelineConfig preCastConfig,
                IReadOnlyList<IAbilityPipelinePhase> preCastPhases,
                IAbilityPipelineConfig castConfig,
                IReadOnlyList<IAbilityPipelinePhase> castPhases,
                object abilityInstance,
                SkillCastRequest request)
            {
                Stage = EntryStage.PreCast;
                Pipeline = null;
                PreCastConfig = preCastConfig;
                PreCastPhases = preCastPhases;
                CastConfig = castConfig;
                CastPhases = castPhases;
                AbilityInstance = abilityInstance;
                Request = request;
            }
        }
    }
}
