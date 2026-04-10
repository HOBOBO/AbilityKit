using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Generic;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Rollback
{
    public sealed class MobaActorTransformRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10001;

        private readonly MobaActorRegistry _registry;

        public MobaActorTransformRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var entries = new List<Entry>(16);
            foreach (var kv in _registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                entries.Add(new Entry(actorId, e.transform.Value));
            }

            entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
            return BinaryObjectCodec.Encode(new Payload(1, entries.ToArray()));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = BinaryObjectCodec.Decode<Payload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (_registry.TryGet(it.ActorId, out var e) && e != null)
                {
                    e.ReplaceTransform(it.Transform);
                }
            }
        }

        public readonly struct Payload
        {
            [BinaryMember(0)] public readonly int Version;
            [BinaryMember(1)] public readonly Entry[] Entries;

            public Payload(int version, Entry[] entries)
            {
                Version = version;
                Entries = entries;
            }
        }

        public readonly struct Entry
        {
            [BinaryMember(0)] public readonly int ActorId;
            [BinaryMember(1)] public readonly Transform3 Transform;

            public Entry(int actorId, Transform3 transform)
            {
                ActorId = actorId;
                Transform = transform;
            }
        }
    }
}
