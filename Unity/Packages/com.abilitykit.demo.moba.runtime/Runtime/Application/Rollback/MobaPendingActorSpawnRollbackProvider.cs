using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Restores only actor-spawn entries that have not been serialized for publication yet.
    /// Submitted snapshots and transport state remain outside rollback.
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaPendingActorSpawnRollbackProvider : IRollbackStateProvider,
        IRollbackStatePreflightProvider, IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 12004;
        private const int Version = 1;
        private const int MaxEntries = 512;

        private readonly MobaActorSpawnSnapshotService _snapshots;

        public MobaPendingActorSpawnRollbackProvider(MobaActorSpawnSnapshotService snapshots)
        {
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            return Write(_snapshots.CapturePendingRollbackState(frame));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            if (state.Frame.Value != frame.Value)
                throw new InvalidOperationException("Pending actor-spawn rollback frame differs from the requested frame.");
        }

        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            var state = Read(context.RequirePayload(DefaultKey).ToArray());
            if (state.Frame.Value != context.Frame.Value)
                throw new InvalidOperationException("Pending actor-spawn snapshot frame differs from the target frame.");
            if (state.Entries.Length == 0) return;

            var actors = MobaEntitasEntityRollbackProvider.ReadSnapshotActorIds(
                context.RequirePayload(MobaEntitasEntityRollbackProvider.DefaultKey).ToArray());
            var versions = MobaEntitasEntityRollbackProvider.ReadSnapshotActorEntityVersions(
                context.RequirePayload(MobaEntitasEntityRollbackProvider.DefaultKey).ToArray());
            var seenActors = new HashSet<int>();
            foreach (var pending in state.Entries)
            {
                var entry = pending.Entry;
                if (!actors.Contains(entry.NetId) || !versions.TryGetValue(entry.NetId, out var version) ||
                    version != entry.EntityVersion)
                    throw new InvalidOperationException(
                        $"Pending actor-spawn entry does not match the target actor roster or entity version. actorId={entry.NetId}");
                if (!seenActors.Add(entry.NetId))
                    throw new InvalidOperationException(
                        $"Pending actor-spawn payload contains duplicate actor identity. actorId={entry.NetId}");
            }
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            if (state.Frame.Value != frame.Value)
                throw new InvalidOperationException("Pending actor-spawn rollback frame differs from the requested frame.");
            _snapshots.RestorePendingRollbackState(state);
        }

        private static byte[] Write(MobaActorSpawnPendingRollbackState state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(state.Frame.Value);
                writer.Write(state.NextSequence);
                writer.Write(state.Entries.Length);
                foreach (var pending in state.Entries)
                {
                    writer.Write(pending.Sequence);
                    var entry = pending.Entry;
                    writer.Write(entry.NetId);
                    writer.Write(entry.Kind);
                    writer.Write(entry.Code);
                    writer.Write(entry.OwnerNetId);
                    writer.Write(entry.X);
                    writer.Write(entry.Y);
                    writer.Write(entry.Z);
                    writer.Write(entry.EntityVersion);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static MobaActorSpawnPendingRollbackState Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing pending actor-spawn rollback payload.");

            using (var stream = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != Version)
                    throw new InvalidOperationException("Unsupported pending actor-spawn rollback payload.");
                var frame = reader.ReadInt32();
                var nextSequence = reader.ReadInt64();
                var count = reader.ReadInt32();
                if (frame < 0 || nextSequence <= 0 || count < 0 || count > MaxEntries)
                    throw new InvalidOperationException("Invalid pending actor-spawn rollback header.");

                var entries = new MobaPendingActorSpawnEntry[count];
                var sequences = new HashSet<long>();
                var actors = new HashSet<int>();
                var previousSequence = 0L;
                for (var i = 0; i < count; i++)
                {
                    var sequence = reader.ReadInt64();
                    var entry = new MobaActorSpawnSnapshotEntry
                    {
                        NetId = reader.ReadInt32(),
                        Kind = reader.ReadInt32(),
                        Code = reader.ReadInt32(),
                        OwnerNetId = reader.ReadInt32(),
                        X = reader.ReadSingle(),
                        Y = reader.ReadSingle(),
                        Z = reader.ReadSingle(),
                        EntityVersion = reader.ReadInt32(),
                    };
                    if (sequence <= previousSequence || sequence >= nextSequence || !sequences.Add(sequence) ||
                        entry.NetId <= 0 || !actors.Add(entry.NetId) || entry.Code <= 0 || entry.OwnerNetId < 0 ||
                        !Enum.IsDefined(typeof(SpawnEntityKind), (SpawnEntityKind)entry.Kind) ||
                        entry.EntityVersion <= 0 || !IsFinite(entry.X) || !IsFinite(entry.Y) || !IsFinite(entry.Z))
                        throw new InvalidOperationException("Invalid or duplicate pending actor-spawn entry.");
                    previousSequence = sequence;
                    entries[i] = new MobaPendingActorSpawnEntry(sequence, in entry);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidOperationException("Unexpected pending actor-spawn payload data.");
                return new MobaActorSpawnPendingRollbackState(new FrameIndex(frame), nextSequence, entries);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
