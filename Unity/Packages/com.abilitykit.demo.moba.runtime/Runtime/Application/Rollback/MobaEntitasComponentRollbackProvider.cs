using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaEntitasComponentRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10018;
        private readonly global::ActorContext _context;
        private readonly bool _allowMissingActorsRestoredByCommands;

        public MobaEntitasComponentRollbackProvider(
            global::ActorContext context,
            bool allowMissingActorsRestoredByCommands = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _allowMissingActorsRestoredByCommands = allowMissingActorsRestoredByCommands;
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var entries = new List<MobaEntitasComponentRollbackEntry>();
            foreach (var entity in _context.GetEntities())
            {
                if (!entity.hasActorId && !entity.hasSkillCastInstanceId) continue;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    Write(entity, writer);
                    entries.Add(new MobaEntitasComponentRollbackEntry(
                        entity.hasActorId ? entity.actorId.Value : 0,
                        entity.hasSkillCastInstanceId ? entity.skillCastInstanceId.Value : 0,
                        stream.ToArray()));
                }
            }
            entries.Sort((a, b) => a.ActorId != b.ActorId
                ? a.ActorId.CompareTo(b.ActorId) : a.CastId.CompareTo(b.CastId));
            return MemoryPackSerializer.Serialize(new MobaEntitasComponentRollbackPayload(1, entries.ToArray()));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            var byActor = new HashSet<int>();
            var byCast = new HashSet<long>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId) byActor.Add(entity.actorId.Value);
                if (entity.hasSkillCastInstanceId) byCast.Add(entity.skillCastInstanceId.Value);
            }
            foreach (var entry in snapshot.Entries ?? Array.Empty<MobaEntitasComponentRollbackEntry>())
            {
                if (entry.Data == null || entry.Data.Length == 0 ||
                    entry.ActorId > 0 && !_allowMissingActorsRestoredByCommands && !byActor.Contains(entry.ActorId) ||
                    entry.CastId > 0 && !byCast.Contains(entry.CastId))
                    throw new InvalidOperationException($"Missing or invalid Entitas component snapshot for actor {entry.ActorId}, cast {entry.CastId}.");
            }
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            foreach (var entry in snapshot.Entries ?? Array.Empty<MobaEntitasComponentRollbackEntry>())
            {
                global::ActorEntity target = null;
                foreach (var entity in _context.GetEntities())
                {
                    if (entry.ActorId > 0 && entity.hasActorId && entity.actorId.Value == entry.ActorId ||
                        entry.ActorId == 0 && entity.hasSkillCastInstanceId && entity.skillCastInstanceId.Value == entry.CastId)
                    {
                        target = entity;
                        break;
                    }
                }
                if (target == null) throw new InvalidOperationException($"Entitas actor {entry.ActorId}, cast {entry.CastId} is missing.");
                using (var stream = new MemoryStream(entry.Data, false))
                using (var reader = new BinaryReader(stream))
                    Restore(target, reader);
            }
        }

        private static void Write(global::ActorEntity e, BinaryWriter w)
        {
            // 2026-10-12 起逐字段序列化由生成器产出（Generated/Actor/ActorComponentSerialization.cs，
            // 覆盖集 = tools/entitas-codegen.moba.pin.json 里 serialize:true 的组件）。
            global::ActorComponentSerialization.Write(e, w);
        }

        private static void Restore(global::ActorEntity e, BinaryReader r)
        {
            global::ActorComponentSerialization.Restore(e, r);
            if (r.BaseStream.Position != r.BaseStream.Length)
                throw new InvalidOperationException("Unexpected Entitas component snapshot trailing data.");
        }

        private static MobaEntitasComponentRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Entitas component snapshot.");
            var snapshot = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(payload);
            if (snapshot.Version != 1) throw new InvalidOperationException("Unsupported Entitas component snapshot version.");
            return snapshot;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaEntitasComponentRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaEntitasComponentRollbackEntry[] Entries;
        [MemoryPackConstructor]
        public MobaEntitasComponentRollbackPayload(int version, MobaEntitasComponentRollbackEntry[] entries)
        { Version = version; Entries = entries; }
    }

    [MemoryPackable]
    public readonly partial struct MobaEntitasComponentRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly long CastId;
        [MemoryPackOrder(2)] public readonly byte[] Data;
        [MemoryPackConstructor]
        public MobaEntitasComponentRollbackEntry(int actorId, long castId, byte[] data)
        { ActorId = actorId; CastId = castId; Data = data; }
    }
}
