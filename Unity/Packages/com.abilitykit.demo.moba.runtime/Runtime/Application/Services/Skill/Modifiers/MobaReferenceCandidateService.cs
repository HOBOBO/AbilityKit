using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaReferenceCandidateService))]
    public sealed class MobaReferenceCandidateService : IService
    {
        private readonly Dictionary<int, List<Candidate>> _byOwner = new Dictionary<int, List<Candidate>>();

        internal void Add(int ownerActorId, int sourceId, int parameterId, int referenceId,
            int priority, int order, in MobaParameterSelector selector)
        {
            if (ownerActorId <= 0 || sourceId == 0 || parameterId <= 0 || referenceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(referenceId));
            if (!_byOwner.TryGetValue(ownerActorId, out var candidates))
                _byOwner[ownerActorId] = candidates = new List<Candidate>(4);
            candidates.Add(new Candidate(sourceId, parameterId, referenceId, priority, order, selector));
        }

        internal void ClearSource(int ownerActorId, int sourceId)
        {
            if (!_byOwner.TryGetValue(ownerActorId, out var candidates)) return;
            candidates.RemoveAll(item => item.SourceId == sourceId);
            if (candidates.Count == 0) _byOwner.Remove(ownerActorId);
        }

        public int Resolve(int ownerActorId, int parameterId, int baseReferenceId,
            in MobaParameterRecord record)
        {
            var expectedStage = MobaParameterStage.None;
            if (!MobaSkillParamModifierKeys.TryResolve(parameterId, out var key) ||
                !MobaSkillParamModifierKeys.TryGetResolveStage(key, out expectedStage) ||
                record.Stage != expectedStage)
                throw new InvalidOperationException(
                    $"Invalid reference candidate stage for {parameterId}: expected {expectedStage}, actual {record.Stage}.");
            if (ownerActorId <= 0 ||
                !_byOwner.TryGetValue(ownerActorId, out var candidates)) return baseReferenceId;
            Candidate? best = null;
            foreach (var candidate in candidates)
            {
                if (candidate.ParameterId != parameterId || !candidate.Selector.Matches(in record)) continue;
                if (!best.HasValue || candidate.Priority > best.Value.Priority ||
                    candidate.Priority == best.Value.Priority &&
                    (candidate.Order < best.Value.Order ||
                     candidate.Order == best.Value.Order && candidate.SourceId < best.Value.SourceId))
                    best = candidate;
            }
            return best?.ReferenceId ?? baseReferenceId;
        }

        public void Dispose() => _byOwner.Clear();

        private readonly struct Candidate
        {
            public Candidate(int sourceId, int parameterId, int referenceId,
                int priority, int order, in MobaParameterSelector selector)
            {
                SourceId = sourceId;
                ParameterId = parameterId;
                ReferenceId = referenceId;
                Priority = priority;
                Order = order;
                Selector = selector;
            }

            public int SourceId { get; }
            public int ParameterId { get; }
            public int ReferenceId { get; }
            public int Priority { get; }
            public int Order { get; }
            public MobaParameterSelector Selector { get; }
        }
    }
}
