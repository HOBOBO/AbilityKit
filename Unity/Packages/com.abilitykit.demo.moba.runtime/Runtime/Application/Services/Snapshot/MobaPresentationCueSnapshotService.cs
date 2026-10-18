using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(65)]
    [WorldService(typeof(MobaPresentationCueSnapshotService))]
    public sealed class MobaPresentationCueSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaSnapshotBuffer<MobaPresentationCueSnapshotEntry> _events = new MobaSnapshotBuffer<MobaPresentationCueSnapshotEntry>(32, 512);
        private readonly MobaActivePresentationCueStore _active = new MobaActivePresentationCueStore();
        private readonly MobaPresentationCueEntryPool _pool = new MobaPresentationCueEntryPool();
        private readonly MobaPresentationCueReplicationPolicy _replication = new MobaPresentationCueReplicationPolicy();
        private readonly MobaPresentationCuePredictionService _prediction = new MobaPresentationCuePredictionService();
        private FrameIndex _lastFrame;
        private long _sequence;
        private long _generation = 1;
        private long _worldId;
        private long _rejectedMissingStableKeyCount;

        public MobaPresentationCueSnapshotService(MobaLogicWorldRunGateService phase)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _lastFrame = new FrameIndex(-999999);
        }

        public IReadOnlyDictionary<string, MobaActivePresentationCue> ActiveCues => _active.Active;
        public int ActiveCueCount => _active.Count;
        public long Generation => _generation;
        public long WorldId => _worldId;
        public long RejectedMissingStableKeyCount => _rejectedMissingStableKeyCount;

        public void SetWorldIdentity(long worldId, long generation)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (_generation != generation)
            {
                _active.Clear();
                _events.ClearAndTrim();
            }

            _worldId = worldId;
            _generation = generation;
        }

        public bool TryGetActiveCue(string key, out MobaActivePresentationCue active)
        {
            return _active.TryGet(key, out active);
        }

        public bool TryGetActiveCue(in MobaPresentationCueSnapshotEntry entry, out MobaActivePresentationCue active)
        {
            return _active.TryGet(in entry, out active);
        }

        public void Report(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            NormalizeMetadata(ref normalized);
            if (MobaPresentationCueKeys.ResolveLifetime(in normalized) == PresentationCueLifetime.PersistentState &&
                string.IsNullOrWhiteSpace(MobaPresentationCueKeys.GetStableKey(in normalized)))
            {
                _rejectedMissingStableKeyCount++;
                return;
            }

            _replication.ApplyDefaults(ref normalized);
            _prediction.ApplyServerAuthoritativeDefaults(ref normalized);
            _active.Observe(in normalized);

            if (_replication.ShouldReplicate(in normalized))
            {
                _events.Add(normalized);
            }
        }

        private void NormalizeMetadata(ref MobaPresentationCueSnapshotEntry entry)
        {
            if (entry.Domain == 0)
            {
                entry.Domain = (int)PresentationCueSemantics.ResolveDomain(entry.OwnerKind ?? entry.CueKind);
            }

            if (entry.Lifetime == 0)
            {
                // Resolve through the key policy so pre-P0 payloads with a
                // stable RequestKey/InstanceKey retain their old persistent
                // lifecycle semantics even when domain was not serialized.
                entry.Lifetime = (int)MobaPresentationCueKeys.ResolveLifetime(in entry);
            }

            entry.WorldId = _worldId;
            entry.Generation = _generation;
            entry.Sequence = ++_sequence;
            entry.Revision = entry.Revision > 0 ? entry.Revision : (int)Math.Min(int.MaxValue, entry.Sequence);
            if (entry.ParameterSchemaVersion == 0) entry.ParameterSchemaVersion = 1;
            if (string.IsNullOrWhiteSpace(entry.EventKey))
            {
                entry.EventKey = !string.IsNullOrWhiteSpace(entry.RequestKey)
                    ? entry.RequestKey
                    : $"event:{_worldId}:{_generation}:{entry.Sequence}";
            }
        }

        public bool TryGetActiveBaseline(out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            var entries = new List<MobaPresentationCueSnapshotEntry>(_active.Count + 1);
            var active = new List<MobaActivePresentationCue>(_active.Count);
            _active.CopyActiveTo(active);
            foreach (var item in active)
            {
                var entry = item.Entry;
                entry.Stage = (int)MobaPresentationCueStage.Started;
                entry.SnapshotKind = (int)PresentationCueSnapshotKind.ActiveBaseline;
                entry.WorldId = _worldId;
                entry.Generation = _generation;
                entry.SourceFrame = _lastFrame.Value;
                entry.Sequence = ++_sequence;
                entry.Revision = item.Revision;
                entries.Add(entry);
            }

            entries.Add(new MobaPresentationCueSnapshotEntry
            {
                SnapshotKind = (int)PresentationCueSnapshotKind.ActiveBaselineEnd,
                WorldId = _worldId,
                Generation = _generation,
                SourceFrame = _lastFrame.Value,
                Sequence = ++_sequence,
            });

            var payloadEntries = _pool.RentExact(entries.Count);
            entries.CopyTo(payloadEntries);
            var payload = MobaPresentationCueSnapshotCodec.Serialize(payloadEntries);
            _pool.Return(payloadEntries);
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.PresentationCue, payload);
            return true;
        }

        public void ReconcileDesiredCueSet(IReadOnlyCollection<MobaPresentationCueSnapshotEntry> desired)
        {
            var incoming = new HashSet<string>(StringComparer.Ordinal);
            if (desired != null)
            {
                foreach (var item in desired)
                {
                    var entry = item;
                    NormalizeMetadata(ref entry);
                    var key = MobaPresentationCueKeys.GetStableKey(in entry);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    incoming.Add(key);
                    _active.Observe(in entry);
                }
            }

            var removals = new List<string>();
            foreach (var key in _active.Active.Keys)
            {
                if (!incoming.Contains(key)) removals.Add(key);
            }

            foreach (var key in removals) _active.Remove(key);
        }

        public void Execute(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Executed;
            Report(in normalized);
        }

        public void Start(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Started;
            Report(in normalized);
        }

        public void Tick(in MobaPresentationCueSnapshotEntry entry, float elapsedSeconds, float remainingSeconds = 0f)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Ticked;
            normalized.ElapsedSeconds = elapsedSeconds;
            normalized.RemainingSeconds = remainingSeconds;
            Report(in normalized);
        }

        public void Refresh(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Refreshed;
            Report(in normalized);
        }

        public void Remove(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Removed;
            Report(in normalized);
        }

        public void Complete(in MobaPresentationCueSnapshotEntry entry)
        {
            var normalized = entry;
            normalized.Stage = (int)MobaPresentationCueStage.Completed;
            Report(in normalized);
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame)
            {
                snapshot = default;
                return false;
            }

            if (frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }

            _lastFrame = frame;

            for (var i = 0; i < _events.Count; i++)
            {
                var entry = _events.Items[i];
                if (entry.SourceFrame == 0) entry.SourceFrame = frame.Value;
                entry.WorldId = _worldId;
                entry.Generation = _generation;
                _events.Items[i] = entry;
            }

            var count = _events.Count;
            if (count == 0)
            {
                snapshot = default;
                return false;
            }

            var payloadEntries = _pool.RentExact(count);
            _events.DrainTo(payloadEntries);
            var payload = MobaPresentationCueSnapshotCodec.Serialize(payloadEntries);
            _pool.Return(payloadEntries);
            snapshot = new WorldStateSnapshot(AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.PresentationCue, payload);
            return true;
        }

        public void Dispose()
        {
            _events.ClearAndTrim();
            _active.Clear();
            _pool.Clear();
            _lastFrame = new FrameIndex(-999999);
            _sequence = 0;
            _rejectedMissingStableKeyCount = 0;
        }
    }
}
