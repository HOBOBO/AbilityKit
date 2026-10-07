using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaBattleDiagnosticAttributeHistory
    {
        void Configure(long actorId, int attributeId, int intervalFrames, bool recording);
        bool TryGetTrack(out long actorId, out int attributeId, out int intervalFrames);
        void Record(BattleDiagnosticActorAttribute attribute);
        IReadOnlyList<BattleDiagnosticActorAttribute> GetSamples();
        void ClearSamples();
        void Stop();
    }

    [WorldService(typeof(IBattleDiagnosticActorAttributeStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticActorAttributeReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticAttributeSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaBattleDiagnosticAttributeHistory), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticActorAttributeStore :
        IBattleDiagnosticActorAttributeStore,
        IBattleDiagnosticAttributeSnapshotSource,
        IMobaBattleDiagnosticAttributeHistory,
        IService
    {
        private const int MaxHistorySamples = 240;
        private readonly BattleDiagnosticActorAttributeStore _store;
        private readonly object _historyLock = new object();
        private readonly List<BattleDiagnosticActorAttribute> _history =
            new List<BattleDiagnosticActorAttribute>(MaxHistorySamples);
        private long _trackedActorId;
        private int _trackedAttributeId;
        private int _intervalFrames = 1;
        private bool _recording;

        public MobaBattleDiagnosticActorAttributeStore(
            MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            _store = new BattleDiagnosticActorAttributeStore(collector.Scope);
        }

        public BattleDiagnosticSessionScope Scope => _store.Scope;
        public long Revision => _store.Revision;
        public int SnapshotFrame => _store.SnapshotFrame;
        public bool IsFrozen => _store.IsFrozen;

        public bool TryReplaceSnapshot(
            int frame,
            System.Collections.Generic.IReadOnlyList<long> actorIds,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorAttribute> attributes,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorAttributeModifier> modifiers)
        {
            return _store.TryReplaceSnapshot(frame, actorIds, attributes, modifiers);
        }

        public BattleDiagnosticAttributeTrackSnapshot CaptureAttributeSnapshot()
        {
            return _store.CaptureAttributeSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorAttributes(requestId, frame, actorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorAttributeModifiers(requestId, frame, actorId);
        }

        public void SetFrozen(bool frozen)
        {
            _store.SetFrozen(frozen);
        }

        public void Clear()
        {
            _store.Clear();
            ClearSamples();
        }

        public void Configure(long actorId, int attributeId, int intervalFrames, bool recording)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (attributeId <= 0) throw new ArgumentOutOfRangeException(nameof(attributeId));
            if (intervalFrames < 1) throw new ArgumentOutOfRangeException(nameof(intervalFrames));
            lock (_historyLock)
            {
                if (_trackedActorId != actorId || _trackedAttributeId != attributeId ||
                    _intervalFrames != intervalFrames)
                    _history.Clear();
                _trackedActorId = actorId;
                _trackedAttributeId = attributeId;
                _intervalFrames = intervalFrames;
                _recording = recording;
            }
        }

        public bool TryGetTrack(out long actorId, out int attributeId, out int intervalFrames)
        {
            lock (_historyLock)
            {
                actorId = _trackedActorId;
                attributeId = _trackedAttributeId;
                intervalFrames = _intervalFrames;
                return _recording && actorId > 0 && attributeId > 0;
            }
        }

        public void Record(BattleDiagnosticActorAttribute attribute)
        {
            lock (_historyLock)
            {
                if (!_recording || !_store.Scope.Equals(attribute.Scope) ||
                    attribute.ActorId != _trackedActorId ||
                    attribute.AttributeId != _trackedAttributeId ||
                    float.IsNaN(attribute.BaseValue) || float.IsInfinity(attribute.BaseValue) ||
                    float.IsNaN(attribute.FinalValue) || float.IsInfinity(attribute.FinalValue))
                    return;
                if (_history.Count > 0)
                {
                    var last = _history[_history.Count - 1];
                    if (attribute.Frame < last.Frame) _history.Clear();
                    else if (attribute.Frame == last.Frame)
                    {
                        _history[_history.Count - 1] = attribute;
                        return;
                    }
                    else if ((long)attribute.Frame - last.Frame < _intervalFrames) return;
                }
                if (_history.Count == MaxHistorySamples) _history.RemoveAt(0);
                _history.Add(attribute);
            }
        }

        public IReadOnlyList<BattleDiagnosticActorAttribute> GetSamples()
        {
            lock (_historyLock) return _history.ToArray();
        }

        public void ClearSamples()
        {
            lock (_historyLock) _history.Clear();
        }

        public void Stop()
        {
            lock (_historyLock)
            {
                _recording = false;
                _trackedActorId = 0;
                _trackedAttributeId = 0;
                _history.Clear();
            }
        }

        public void Dispose()
        {
        }
    }
}
