using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaReferenceCandidateProjector : IMobaContinuousModifierProjector
    {
        private MobaReferenceCandidateService _candidates;
        private MobaConfigDatabase _configs;

        public int TargetKind => MobaContinuousModifierTargetKind.ParameterReferenceCandidate;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _candidates);
            services?.TryResolve(out _configs);
        }

        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers)
        {
            if (projection == null || modifiers == null) return;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var spec = modifiers[i];
                if (spec?.TargetKind != TargetKind) continue;
                if (_candidates == null)
                    throw new InvalidOperationException("Reference candidate service is unavailable.");
                if (!(spec is IMobaContinuousReferenceCandidateSpec candidate) ||
                    candidate.CandidateReferenceId <= 0 ||
                    !(spec is IMobaContinuousParameterSelectorSpec selected) ||
                    !MobaSkillParamModifierKeys.TryResolve(spec.TargetId, out var key) ||
                    !MobaSkillParamModifierKeys.TryGetResolveStage(key, out var stage) ||
                    !selected.Selector.IsValidForStage(stage) ||
                    !IsValidReference(spec.TargetId, candidate.CandidateReferenceId))
                    throw new InvalidOperationException("Invalid parameter reference candidate.");
                _candidates.Add(projection.OwnerActorId, projection.ModifierSourceId,
                    spec.TargetId, candidate.CandidateReferenceId, spec.Priority, i, selected.Selector);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection != null)
                _candidates?.ClearSource(projection.OwnerActorId, projection.ModifierSourceId);
        }

        private bool IsValidReference(int parameterId, int referenceId)
        {
            if (!MobaSkillParamModifierKeys.TryResolve(parameterId, out var key)) return false;
            if (key.Equals(MobaSkillParamModifierKeys.Emitter.LauncherId))
                return _configs == null || _configs.TryGetProjectileLauncher(referenceId, out _);
            if (key.Equals(MobaSkillParamModifierKeys.Projectile.ProjectileId))
                return _configs == null || _configs.TryGetProjectile(referenceId, out _);
            if (key.Equals(MobaSkillParamModifierKeys.Summon.SummonId))
                return _configs == null || _configs.TryGetSummon(referenceId, out _);
            return false;
        }
    }
}
