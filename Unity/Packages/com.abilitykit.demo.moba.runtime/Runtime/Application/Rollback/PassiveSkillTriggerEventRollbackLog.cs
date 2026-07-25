using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class PassiveSkillTriggerEventRollbackLog : IRollbackStateProvider
    {
        public const int DefaultKey = 10020;

        private static readonly ObjectPool<FrameEvents> s_frameEventsPool = Pools.GetPool(
            createFunc: () => new FrameEvents(),
            onRelease: events => events.Clear(),
            defaultCapacity: 32,
            maxSize: 512,
            collectionCheck: false);

        private static readonly ObjectPool<List<int>> s_intListPool = Pools.GetPool(
            createFunc: () => new List<int>(128),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly Dictionary<int, FrameEvents> _eventsByFrame = new Dictionary<int, FrameEvents>(256);

        public int Key => DefaultKey;

        public void Record(FrameIndex frame, in PassiveSkillTriggerEventArgs args)
        {
            var f = frame.Value;
            if (!_eventsByFrame.TryGetValue(f, out var fe))
            {
                fe = s_frameEventsPool.Get();
                _eventsByFrame[f] = fe;
            }

            fe.Sequence++;
            fe.Events.Add(new MobaPassiveTriggerEventRollbackEntry(fe.Sequence, in args));
        }

        public IReadOnlyList<MobaPassiveTriggerEventRollbackEntry> GetFrameEvents(FrameIndex frame)
        {
            return _eventsByFrame.TryGetValue(frame.Value, out var fe) ? fe.Events : Array.Empty<MobaPassiveTriggerEventRollbackEntry>();
        }

        public void TruncateAfter(FrameIndex frame)
        {
            var cutoff = frame.Value;
            if (_eventsByFrame.Count == 0) return;

            var tmpKeys = s_intListPool.Get();
            try
            {
                foreach (var kv in _eventsByFrame)
                {
                    if (kv.Key > cutoff) tmpKeys.Add(kv.Key);
                }

                for (int i = 0; i < tmpKeys.Count; i++)
                {
                    RemoveFrameEvents(tmpKeys[i]);
                }
            }
            finally
            {
                s_intListPool.Release(tmpKeys);
            }
        }

        public byte[] Export(FrameIndex frame)
        {
            if (!_eventsByFrame.TryGetValue(frame.Value, out var fe) || fe.Events.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var arr = fe.Events.ToArray();
            return MemoryPackSerializer.Serialize(new MobaPassiveTriggerEventRollbackPayload(1, fe.Sequence, arr));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            TruncateAfter(frame);

            if (payload == null || payload.Length == 0)
            {
                RemoveFrameEvents(frame.Value);
                return;
            }

            var p = MemoryPackSerializer.Deserialize<MobaPassiveTriggerEventRollbackPayload>(payload);
            if (p.Events == null || p.Events.Length == 0)
            {
                RemoveFrameEvents(frame.Value);
                return;
            }

            RemoveFrameEvents(frame.Value);

            var fe = s_frameEventsPool.Get();
            fe.Sequence = p.LastSequence;
            fe.Events.AddRange(p.Events);
            _eventsByFrame[frame.Value] = fe;
        }

        private void RemoveFrameEvents(int frame)
        {
            if (!_eventsByFrame.TryGetValue(frame, out var fe)) return;

            _eventsByFrame.Remove(frame);
            s_frameEventsPool.Release(fe);
        }

        private sealed class FrameEvents
        {
            public int Sequence;
            public readonly List<MobaPassiveTriggerEventRollbackEntry> Events = new List<MobaPassiveTriggerEventRollbackEntry>(8);

            public void Clear()
            {
                Sequence = 0;
                Events.Clear();
            }
        }


    }



    [MemoryPackable]
    public readonly partial struct MobaPassiveTriggerEventRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int LastSequence;
        [MemoryPackOrder(2)] public readonly MobaPassiveTriggerEventRollbackEntry[] Events;

        public MobaPassiveTriggerEventRollbackPayload(int version, int lastSequence, MobaPassiveTriggerEventRollbackEntry[] events)
        {
            Version = version;
            LastSequence = lastSequence;
            Events = events;
        }
    }


    [MemoryPackable]
    public readonly partial struct MobaPassiveTriggerEventRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int Sequence;
        [MemoryPackOrder(1)] public readonly PassiveSkillTriggerEventArgs Args;

        public MobaPassiveTriggerEventRollbackEntry(int sequence, in PassiveSkillTriggerEventArgs args)
        {
            Sequence = sequence;
            Args = args;
        }
    }

}
