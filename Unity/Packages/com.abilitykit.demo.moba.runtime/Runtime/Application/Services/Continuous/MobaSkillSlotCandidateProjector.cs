using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillSlotCandidateProjector : IMobaContinuousModifierProjector
    {
        private MobaSkillSlotCandidateService _candidates;
        private MobaConfigDatabase _configs;

        public int TargetKind => MobaContinuousModifierTargetKind.SkillSlotCandidate;

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
                    throw new System.InvalidOperationException("Skill slot candidate service is unavailable.");
                if (!(spec is IMobaContinuousSkillCandidateSpec candidate) ||
                    projection.OwnerActorId <= 0 || projection.ModifierSourceId == 0 ||
                    spec.TargetId <= 0 || candidate.CandidateSkillId <= 0)
                    throw new System.InvalidOperationException("Invalid skill slot candidate.");
                if (_configs != null && !_configs.TryGetSkill(candidate.CandidateSkillId, out _))
                    throw new System.InvalidOperationException($"Unknown candidate skill {candidate.CandidateSkillId}.");
                _candidates.Add(projection.OwnerActorId, projection.ModifierSourceId,
                    spec.TargetId, candidate.CandidateSkillId, spec.Priority, i);
            }
        }

        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            if (projection != null)
                _candidates?.ClearSource(projection.OwnerActorId, projection.ModifierSourceId);
        }
    }
}
