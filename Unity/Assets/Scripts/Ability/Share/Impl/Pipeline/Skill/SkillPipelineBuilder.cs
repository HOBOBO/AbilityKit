using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Pipeline.Timeline;
using AbilityKit.ActionSchema;

namespace AbilityKit.Ability.Share.Impl.Pipeline.Skill
{
    public sealed class SkillPipelineBuilder
    {
        private readonly SkillPipelineConfig _config;

        public SkillPipelineBuilder(int configId, string configName, bool allowInterrupt = true, bool allowPause = true)
        {
            _config = new SkillPipelineConfig(configId, configName, allowInterrupt, allowPause);
        }

        public SkillPipelineConfig Build() => _config;

        public SkillPipelineBuilder Condition(string name, Func<IAbilityPipelineContext, bool> predicate, string failMessage = null)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new PredicatePhaseConfig(id, SkillPipelinePhaseTypes.Condition, predicate, failMessage));
            return this;
        }

        public SkillPipelineBuilder Cost(string name, Func<IAbilityPipelineContext, bool> tryPay, string failMessage = null)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new PredicatePhaseConfig(id, SkillPipelinePhaseTypes.Cost, tryPay, failMessage));
            return this;
        }

        public SkillPipelineBuilder Check(string name, Func<IAbilityPipelineContext, bool> checker, string failMessage = null)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new PredicatePhaseConfig(id, SkillPipelinePhaseTypes.Check, checker, failMessage));
            return this;
        }

        public SkillPipelineBuilder Timeline(string name, SkillAssetDto asset)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new TimelinePhaseConfig(id, asset));
            return this;
        }

        public SkillPipelineBuilder RecoverWait(string name, float duration)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new RecoverPhaseConfig(id, duration));
            return this;
        }

        public SkillPipelineBuilder CustomInstant(string name, Action<IAbilityPipelineContext> action)
        {
            var id = AbilityPipelinePhaseIdManager.Instance.Register(name);
            _config.AddPhase(new ActionPhaseConfig(id, action));
            return this;
        }

        private sealed class PredicatePhaseConfig : PhaseConfigBase
        {
            public readonly Func<IAbilityPipelineContext, bool> Predicate;
            public readonly string FailMessage;

            public PredicatePhaseConfig(AbilityPipelinePhaseId phaseId, string phaseType, Func<IAbilityPipelineContext, bool> predicate, string failMessage)
                : base(phaseId, phaseType)
            {
                Predicate = predicate;
                FailMessage = failMessage;
            }
        }

        private sealed class TimelinePhaseConfig : PhaseConfigBase
        {
            public readonly SkillAssetDto Asset;

            public TimelinePhaseConfig(AbilityPipelinePhaseId phaseId, SkillAssetDto asset)
                : base(phaseId, SkillPipelinePhaseTypes.Timeline, asset != null ? asset.length : 0)
            {
                Asset = asset;
            }
        }

        private sealed class RecoverPhaseConfig : PhaseConfigBase
        {
            public RecoverPhaseConfig(AbilityPipelinePhaseId phaseId, float duration)
                : base(phaseId, SkillPipelinePhaseTypes.Recover, duration)
            {
            }
        }

        private sealed class ActionPhaseConfig : PhaseConfigBase
        {
            public readonly Action<IAbilityPipelineContext> Action;

            public ActionPhaseConfig(AbilityPipelinePhaseId phaseId, Action<IAbilityPipelineContext> action)
                : base(phaseId, SkillPipelinePhaseTypes.Custom)
            {
                Action = action;
            }
        }

        public IReadOnlyList<IAbilityPipelinePhase> CreatePhases()
        {
            var phases = new List<IAbilityPipelinePhase>(_config.PhaseConfigs.Count);
            foreach (var pc in _config.PhaseConfigs)
            {
                phases.Add(SkillPipelinePhaseFactory.Create(pc));
            }

            return phases;
        }

        private static class SkillPipelinePhaseFactory
        {
            public static IAbilityPipelinePhase Create(IAbilityPhaseConfig config)
            {
                if (config is PredicatePhaseConfig predicate)
                {
                    return new PredicateInstantPhase(predicate.PhaseId, predicate.Predicate, predicate.FailMessage);
                }

                if (config is TimelinePhaseConfig timeline)
                {
                    return new AbilityTimelinePhase(config.PhaseId, timeline.Asset);
                }

                if (config is RecoverPhaseConfig recover)
                {
                    return new RecoverWaitPhase(recover.PhaseId, recover.Duration);
                }

                if (config is ActionPhaseConfig action)
                {
                    return new ActionInstantPhase(action.PhaseId, action.Action);
                }

                return new UnsupportedPhase(config.PhaseId, config.PhaseType);
            }

            private sealed class UnsupportedPhase : AbilityInstantPhaseBase
            {
                private readonly string _phaseType;

                public UnsupportedPhase(AbilityPipelinePhaseId phaseId, string phaseType)
                    : base(phaseId)
                {
                    _phaseType = phaseType;
                }

                protected override void OnInstantExecute(IAbilityPipelineContext context)
                {
                    throw new InvalidOperationException($"Unsupported phase type: '{_phaseType}' (PhaseId={PhaseId})");
                }
            }

            private sealed class PredicateInstantPhase : AbilityInstantPhaseBase
            {
                private readonly Func<IAbilityPipelineContext, bool> _predicate;
                private readonly string _failMessage;

                public PredicateInstantPhase(AbilityPipelinePhaseId phaseId, Func<IAbilityPipelineContext, bool> predicate, string failMessage)
                    : base(phaseId)
                {
                    _predicate = predicate;
                    _failMessage = failMessage;
                }

                protected override void OnInstantExecute(IAbilityPipelineContext context)
                {
                    if (_predicate == null) return;
                    if (_predicate(context)) return;

                    throw new InvalidOperationException(_failMessage ?? $"Phase predicate failed: {PhaseId}");
                }
            }

            private sealed class ActionInstantPhase : AbilityInstantPhaseBase
            {
                private readonly Action<IAbilityPipelineContext> _action;

                public ActionInstantPhase(AbilityPipelinePhaseId phaseId, Action<IAbilityPipelineContext> action)
                    : base(phaseId)
                {
                    _action = action;
                }

                protected override void OnInstantExecute(IAbilityPipelineContext context)
                {
                    _action?.Invoke(context);
                }
            }

            private sealed class RecoverWaitPhase : AbilityDurationalPhaseBase
            {
                public RecoverWaitPhase(AbilityPipelinePhaseId phaseId, float duration)
                    : base(phaseId)
                {
                    Duration = duration;
                }

                protected override void OnExecute(IAbilityPipelineContext context)
                {
                    // wait until Duration elapses
                }
            }
        }
    }
}
