using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Share;
using AbilityKit.Ability.Share.Impl.Moba.Move;

namespace AbilityKit.Ability.Share.Impl.Moba.Rollback
{
    public sealed class MobaMoveRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10002;

        private readonly MobaMoveService _moves;

        public MobaMoveRollbackProvider(MobaMoveService moves)
        {
            _moves = moves ?? throw new ArgumentNullException(nameof(moves));
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var entries = _moves.ExportAll();
            var items = new Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                items[i] = new Entry(entries[i].actorId, entries[i].snapshot);
            }

            return BinaryObjectCodec.Encode(new Payload(1, items));
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = BinaryObjectCodec.Decode<Payload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            var arr = new (int actorId, MoveController.ControllerSnapshot snapshot)[p.Entries.Length];
            for (int i = 0; i < p.Entries.Length; i++)
            {
                arr[i] = (p.Entries[i].ActorId, p.Entries[i].Snapshot);
            }

            _moves.ImportAll(arr);
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
            [BinaryMember(1)] public readonly MoveController.ControllerSnapshot Snapshot;

            public Entry(int actorId, in MoveController.ControllerSnapshot snapshot)
            {
                ActorId = actorId;
                Snapshot = snapshot;
            }
        }
    }
}
