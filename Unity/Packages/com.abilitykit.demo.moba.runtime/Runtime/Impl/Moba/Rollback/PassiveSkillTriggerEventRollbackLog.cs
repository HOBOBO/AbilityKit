using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Share;
using AbilityKit.Ability.Share.Impl.Moba.Systems;

namespace AbilityKit.Ability.Share.Impl.Moba.Rollback
{
    public sealed class PassiveSkillTriggerEventRollbackLog : IRollbackStateProvider
    {
        public const int DefaultKey = 10020;

        private readonly Dictionary<int, FrameEvents> _eventsByFrame = new Dictionary<int, FrameEvents>(256);

        public int Key => DefaultKey;

        public void Record(FrameIndex frame, in PassiveSkillTriggerEventArgs args)
        {
            var f = frame.Value;
            if (!_eventsByFrame.TryGetValue(f, out var fe))
            {
                fe = new FrameEvents();
                _eventsByFrame[f] = fe;
            }

            fe.Sequence++;
            fe.Events.Add(new Entry(fe.Sequence, in args));
        }

        public IReadOnlyList<Entry> GetFrameEvents(FrameIndex frame)
        {
            return _eventsByFrame.TryGetValue(frame.Value, out var fe) ? fe.Events : Array.Empty<Entry>();
        }

        public void TruncateAfter(FrameIndex frame)
        {
            var cutoff = frame.Value;
            if (_eventsByFrame.Count == 0) return;

            s_tmpKeys.Clear();
            foreach (var kv in _eventsByFrame)
            {
                if (kv.Key > cutoff) s_tmpKeys.Add(kv.Key);
            }

            for (int i = 0; i < s_tmpKeys.Count; i++)
            {
                _eventsByFrame.Remove(s_tmpKeys[i]);
            }

            s_tmpKeys.Clear();
        }

        public byte[] Export(FrameIndex frame)
        {
            if (!_eventsByFrame.TryGetValue(frame.Value, out var fe) || fe.Events.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var arr = fe.Events.ToArray();
            return BinaryObjectCodec.Encode(new Payload(1, fe.Sequence, arr));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            TruncateAfter(frame);

            if (payload == null || payload.Length == 0)
            {
                _eventsByFrame.Remove(frame.Value);
                return;
            }

            var p = BinaryObjectCodec.Decode<Payload>(payload);
            if (p.Events == null || p.Events.Length == 0)
            {
                _eventsByFrame.Remove(frame.Value);
                return;
            }

            var fe = new FrameEvents();
            fe.Sequence = p.LastSequence;
            fe.Events.AddRange(p.Events);
            _eventsByFrame[frame.Value] = fe;
        }

        private sealed class FrameEvents
        {
            public int Sequence;
            public readonly List<Entry> Events = new List<Entry>(8);
        }

        private static readonly List<int> s_tmpKeys = new List<int>(128);

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly int LastSequence;
            [BinaryMember(2)] public readonly Entry[] Events;

            public Payload(int version, int lastSequence, Entry[] events)
            {
                Version = version;
                LastSequence = lastSequence;
                Events = events;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int Sequence;
            [BinaryMember(1)] public readonly PassiveSkillTriggerEventArgs Args;

            public Entry(int sequence, in PassiveSkillTriggerEventArgs args)
            {
                Sequence = sequence;
                Args = args;
            }
        }
    }
}
