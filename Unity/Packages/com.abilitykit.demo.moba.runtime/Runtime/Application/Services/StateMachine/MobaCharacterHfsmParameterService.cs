using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.HFSM.Definition;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    /// <summary>Source-owned parameter contributions consumed by character HFSM conditions.</summary>
    [WorldService(typeof(MobaCharacterHfsmParameterService))]
    public sealed class MobaCharacterHfsmParameterService : IService
    {
        public const int Controlled = 1;
        public const int Casting = 2;
        public const int Moving = 3;
        public static bool IsValidParameterId(int parameterId)
        {
            return parameterId > 0;
        }

        public static bool IsValidOperation(int parameterId, int operation)
        {
            return IsValidParameterId(parameterId) &&
                   operation >= (int)ModifierOp.Add && operation <= (int)ModifierOp.PercentAdd;
        }

        public bool SupportsOperation(int actorId, int parameterId, int operation)
        {
            if (!IsValidOperation(parameterId, operation)) return false;
            return _profileParameters.TryGetValue(actorId, out var declared) &&
                   declared.TryGetValue(parameterId, out var operations) && operations.Contains(operation);
        }

        public bool UsesProfile(int actorId, string definitionId) =>
            actorId > 0 && !string.IsNullOrEmpty(definitionId) &&
            _profileIds.TryGetValue(actorId, out var currentProfileId) &&
            string.Equals(currentProfileId, definitionId, StringComparison.Ordinal);

        private readonly Dictionary<int, Dictionary<int, List<Contribution>>> _byActor =
            new Dictionary<int, Dictionary<int, List<Contribution>>>();
        private readonly Dictionary<int, Dictionary<int, HashSet<int>>> _profileParameters =
            new Dictionary<int, Dictionary<int, HashSet<int>>>();
        private readonly Dictionary<int, string> _profileIds = new Dictionary<int, string>();
        private readonly ModifierCalculator _calculator = new ModifierCalculator { EnableCache = false };

        private readonly struct Contribution
        {
            public readonly long OwnerId;
            public readonly ModifierData Modifier;
            public Contribution(long ownerId, in ModifierData modifier)
            { OwnerId = ownerId; Modifier = modifier; }
        }

        public void Set(int actorId, int sourceId, int parameterId, in ModifierData modifier)
            => Set(actorId, (long)sourceId, parameterId, in modifier);

        /// <summary>Sets a contribution owned by a runtime instance. The owner identity is
        /// deliberately wider than ModifierData.SourceId so concurrent casts cannot alias.</summary>
        public void Set(int actorId, long sourceId, int parameterId, in ModifierData modifier)
        {
            if (actorId <= 0 || sourceId <= 0 || parameterId <= 0)
                throw new ArgumentOutOfRangeException("HFSM parameter owner, source and target ids must be positive.");
            if (!_profileParameters.TryGetValue(actorId, out var declared))
                throw new InvalidOperationException($"Actor {actorId} has no registered HFSM parameter profile.");
            if (!declared.TryGetValue(parameterId, out var operations))
                throw new InvalidOperationException($"HFSM parameter {parameterId} is not declared by actor {actorId}'s profile.");
            if (!IsValidOperation(parameterId, (int)modifier.Op) || !operations.Contains((int)modifier.Op))
                throw new InvalidOperationException(
                    $"HFSM parameter {parameterId} does not allow modifier operation {(int)modifier.Op} for actor {actorId}.");
            if (!_byActor.TryGetValue(actorId, out var byParameter))
                _byActor[actorId] = byParameter = new Dictionary<int, List<Contribution>>();
            if (!byParameter.TryGetValue(parameterId, out var list))
                byParameter[parameterId] = list = new List<Contribution>(2);
            for (var i = list.Count - 1; i >= 0; i--)
                if (list[i].OwnerId == sourceId) list.RemoveAt(i);
            list.Add(new Contribution(sourceId, in modifier));
        }

        /// <summary>
        /// Toggles a profile-local enum parameter between two configured values.
        /// The contribution is owned by a stable gameplay source (for example, a
        /// skill definition), so repeated casts replace it instead of leaving one
        /// persistent contribution per cast. Unknown effective values are rejected.
        /// </summary>
        public bool TryToggleOverride(
            int actorId,
            long ownerId,
            int parameterId,
            float firstValue,
            float secondValue,
            int sourceId,
            int priority = 0)
        {
            if (actorId <= 0 || ownerId <= 0 || sourceId <= 0 ||
                !SupportsOperation(actorId, parameterId, (int)ModifierOp.Override) ||
                firstValue == secondValue)
                return false;

            var effectiveValue = Resolve(actorId, parameterId);
            float nextValue;
            if (effectiveValue == firstValue) nextValue = secondValue;
            else if (effectiveValue == secondValue) nextValue = firstValue;
            else return false;

            var modifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)parameterId), nextValue, sourceId: sourceId);
            modifier.Priority = priority;
            Set(actorId, ownerId, parameterId, in modifier);
            return true;
        }

        public void ClearSource(int actorId, int sourceId)
            => ClearSource(actorId, (long)sourceId);

        public void ClearSource(int actorId, long sourceId)
        {
            if (!_byActor.TryGetValue(actorId, out var byParameter)) return;
            var empty = new List<int>();
            foreach (var pair in byParameter)
            {
                pair.Value.RemoveAll(x => x.OwnerId == sourceId);
                if (pair.Value.Count == 0) empty.Add(pair.Key);
            }
            for (var i = 0; i < empty.Count; i++) byParameter.Remove(empty[i]);
            if (byParameter.Count == 0) _byActor.Remove(actorId);
        }

        public float Resolve(int actorId, int parameterId, float baseValue = 0f)
        {
            if (!_byActor.TryGetValue(actorId, out var byParameter) ||
                !byParameter.TryGetValue(parameterId, out var modifiers) || modifiers.Count == 0)
                return baseValue;
            modifiers.Sort(CompareContributions);
            var data = new ModifierData[modifiers.Count];
            for (var i = 0; i < data.Length; i++) data[i] = modifiers[i].Modifier;
            return _calculator.Calculate(data.AsSpan(), baseValue).FinalValue;
        }

        public bool ResolveBool(int actorId, int parameterId, bool baseValue = false)
        {
            return Resolve(actorId, parameterId, baseValue ? 1f : 0f) > 0.5f;
        }

        public sealed class SnapshotEntry
        {
            public int ActorId;
            public long OwnerId;
            public int ParameterId;
            public ModifierData Modifier;
            public SnapshotEntry() { }
            public SnapshotEntry(int actorId, long ownerId, int parameterId, in ModifierData modifier)
            { ActorId = actorId; OwnerId = ownerId; ParameterId = parameterId; Modifier = modifier; }
        }

        public SnapshotEntry[] CaptureSnapshot()
        {
            var result = new List<SnapshotEntry>();
            foreach (var actor in _byActor)
                foreach (var parameter in actor.Value)
                    foreach (var contribution in parameter.Value)
                        result.Add(new SnapshotEntry(actor.Key, contribution.OwnerId, parameter.Key, contribution.Modifier));
            return result.ToArray();
        }

        public void RestoreSnapshot(IReadOnlyList<SnapshotEntry> entries)
        {
            _byActor.Clear();
            if (entries == null) return;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Set(entry.ActorId, entry.OwnerId, entry.ParameterId, in entry.Modifier);
            }
        }

        private static int CompareContributions(Contribution left, Contribution right)
        {
            var priority = left.Modifier.Priority.CompareTo(right.Modifier.Priority);
            return priority != 0 ? priority : left.OwnerId.CompareTo(right.OwnerId);
        }

        public void Dispose()
        {
            _byActor.Clear();
            _profileParameters.Clear();
            _profileIds.Clear();
        }

        public void RegisterActorProfile(int actorId, StateMachineDefinition definition)
        {
            if (actorId <= 0) return;
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var parameters = new Dictionary<int, HashSet<int>>();
            foreach (var parameter in definition?.Parameters ?? new List<StateMachineParameterDefinition>())
                if (parameter != null && parameter.Id > 0)
                    parameters[parameter.Id] = new HashSet<int>(parameter.SupportedOperations ?? new List<int>());

            if (_profileIds.TryGetValue(actorId, out var currentProfileId) &&
                !string.Equals(currentProfileId, definition.DefinitionId, StringComparison.Ordinal))
            {
                _byActor.Remove(actorId);
            }
            else if (_byActor.TryGetValue(actorId, out var currentValues))
            {
                foreach (var parameter in currentValues)
                {
                    if (!parameters.TryGetValue(parameter.Key, out var operations))
                        throw new InvalidOperationException(
                            $"Actor {actorId} already has a contribution for undeclared HFSM parameter {parameter.Key}.");
                    foreach (var contribution in parameter.Value)
                        if (!operations.Contains((int)contribution.Modifier.Op))
                            throw new InvalidOperationException(
                                $"Actor {actorId} already has a contribution using unsupported operation {(int)contribution.Modifier.Op} for HFSM parameter {parameter.Key}.");
                }
            }

            _profileParameters[actorId] = parameters;
            _profileIds[actorId] = definition.DefinitionId ?? string.Empty;
        }

        public bool IsDeclared(int actorId, int parameterId)
        {
            return _profileParameters.TryGetValue(actorId, out var parameters) && parameters.ContainsKey(parameterId);
        }

        public void ClearActorProfile(int actorId)
        {
            _profileParameters.Remove(actorId);
            _profileIds.Remove(actorId);
            _byActor.Remove(actorId);
        }
    }
}
